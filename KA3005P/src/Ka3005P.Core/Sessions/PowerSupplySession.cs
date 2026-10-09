using Ka3005P.Core.Device;
using Ka3005P.Core.Measurements;
using Ka3005P.Core.Protocol;

namespace Ka3005P.Core.Sessions;

public sealed class PowerSupplySession : IPowerSupplySession
{
	private const int MaximumOperationAttempts=3;
	private static readonly TimeSpan RetryDelay=TimeSpan.FromMilliseconds(100);
	private readonly IPowerSupplyDevice device;
	private readonly TimeProvider timeProvider;
	private readonly TimeSpan pollingInterval;
	private readonly SessionRequestQueue requests=new();
	private readonly CancellationTokenSource lifetime=new();
	private readonly object snapshotGate=new();
	private readonly object lifecycleGate=new();
	private SessionSnapshot snapshot=new();
	private Task? runTask;
	private Task? pollingTask;
	private long sessionStartedTimestamp;
	private bool disposed;

	public PowerSupplySession(IPowerSupplyDevice device,TimeProvider timeProvider)
		: this(device,timeProvider,TimeSpan.FromMilliseconds(250))
	{
	}

	public PowerSupplySession(
		IPowerSupplyDevice device,
		TimeProvider timeProvider,
		TimeSpan pollingInterval)
	{
		ArgumentNullException.ThrowIfNull(device);
		ArgumentNullException.ThrowIfNull(timeProvider);
		if(pollingInterval <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(pollingInterval));
		}
		this.device=device;
		this.timeProvider=timeProvider;
		this.pollingInterval=pollingInterval;
	}

	public event EventHandler<SessionSnapshot>? SnapshotChanged;
	public event EventHandler<MeasurementSample>? MeasurementReceived;

	public SessionSnapshot Snapshot
	{
		get
		{
			lock(snapshotGate)
			{
				if(snapshot.LastMeasurementTimestamp is not long timestamp)
				{
					return snapshot;
				}

				return snapshot with
				{
					MeasurementAge=timeProvider.GetElapsedTime(
						timestamp,
						timeProvider.GetTimestamp())
				};
			}
		}
	}

	public int PendingSetpointCount => requests.PendingSetpointCount;

	public ValueTask StartAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(disposed,this);
		cancellationToken.ThrowIfCancellationRequested();
		lock(lifecycleGate)
		{
			if(runTask is not null)
			{
				throw new InvalidOperationException("Sesja została już uruchomiona.");
			}

			sessionStartedTimestamp=timeProvider.GetTimestamp();
			runTask=RunAsync();
			pollingTask=PollMeasurementsAsync(lifetime.Token);
		}
		Publish(current=>current with { IsRunning=true });
		return ValueTask.CompletedTask;
	}

	public async ValueTask StopAsync(CancellationToken cancellationToken)
	{
		Task? running;
		lock(lifecycleGate)
		{
			running=runTask;
		}

		if(running is null)
		{
			return;
		}

		requests.Close();
		await running.WaitAsync(cancellationToken).ConfigureAwait(false);
		lifetime.Cancel();
		Task? polling;
		lock(lifecycleGate)
		{
			polling=pollingTask;
		}
		if(polling is not null)
		{
			try
			{
				await polling.WaitAsync(cancellationToken).ConfigureAwait(false);
			}
			catch(OperationCanceledException) when(lifetime.IsCancellationRequested)
			{
			}
		}
	}

	public void RequestVoltage(VoltageSetpoint value)
	{
		EnsureRunning();
		Publish(current=>current with
		{
			RequestedVoltage=value,
			Error=null
		});
		requests.SetVoltage(value);
	}

	public void RequestCurrent(CurrentSetpoint value)
	{
		EnsureRunning();
		Publish(current=>current with
		{
			RequestedCurrent=value,
			Error=null
		});
		requests.SetCurrent(value);
	}

	public void RequestMeasurement()
	{
		EnsureRunning();
		requests.RequestMeasurement();
	}

	public async ValueTask SetOutputAsync(
		bool enabled,
		CancellationToken cancellationToken)
	{
		EnsureRunning();
		Publish(current=>current with
		{
			RequestedOutput=enabled,
			Error=null
		});
		SetOutputRequest request=requests.SetOutput(enabled);
		await request.Completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
	}

	public async ValueTask DisposeAsync()
	{
		if(disposed)
		{
			return;
		}

		disposed=true;
		await StopAsync(CancellationToken.None).ConfigureAwait(false);
		await device.DisposeAsync().ConfigureAwait(false);
		lifetime.Dispose();
	}

	private async Task RunAsync()
	{
		try
		{
			while(true)
			{
				await requests.WaitAsync(CancellationToken.None).ConfigureAwait(false);
				while(requests.TryTakeNext(out SessionRequest? request))
				{
					if(request is CloseSessionRequest)
					{
						return;
					}

					await ExecuteAsync(request).ConfigureAwait(false);
				}
			}
		}
		finally
		{
			Publish(current=>current with { IsRunning=false });
		}
	}

	private async ValueTask ExecuteAsync(SessionRequest request)
	{
		Exception? finalException=null;
		for(int attempt=1;attempt<=MaximumOperationAttempts;attempt++)
		{
			try
			{
				await ExecuteOnceAsync(request).ConfigureAwait(false);
				return;
			}
			catch(DeviceCommunicationException exception)
				when(attempt<MaximumOperationAttempts)
			{
				finalException=exception;
				await Task.Delay(RetryDelay).ConfigureAwait(false);
			}
			catch(Exception exception)
			{
				finalException=exception;
				break;
			}
		}

		Exception failure=finalException ??
			new DeviceCommunicationException("Nieznany błąd komunikacji z urządzeniem.");
		Publish(current=>current with
		{
			Error=new SessionError(
				timeProvider.GetUtcNow(),
				failure.Message,
				failure)
		});
		if(request is SetOutputRequest output)
		{
			output.Completion.TrySetException(failure);
		}
		if(request is MeasurementRequest measurement)
		{
			measurement.Completion.TrySetResult();
		}
	}

	private async ValueTask ExecuteOnceAsync(SessionRequest request)
	{
		switch(request)
		{
			case SetVoltageRequest voltage:
				await device.SetVoltageAsync(
					voltage.Value,
					CancellationToken.None).ConfigureAwait(false);
				Publish(current=>current with
				{
					SentVoltage=voltage.Value,
					Error=null
				});
				break;
			case SetCurrentRequest current:
				await device.SetCurrentAsync(
					current.Value,
					CancellationToken.None).ConfigureAwait(false);
				Publish(value=>value with
				{
					SentCurrent=current.Value,
					Error=null
				});
				break;
			case SetOutputRequest output:
				await device.SetOutputAsync(
					output.Enabled,
					CancellationToken.None).ConfigureAwait(false);
				Publish(current=>current with
				{
					OutputState=output.Enabled ? OutputState.On : OutputState.Off,
					Error=null
				});
				output.Completion.TrySetResult();
				break;
			case MeasurementRequest measurementRequest:
				DeviceMeasurement measurement=
					await device.ReadMeasurementAsync(CancellationToken.None).ConfigureAwait(false);
				long timestamp=timeProvider.GetTimestamp();
				DateTimeOffset recordedAt=timeProvider.GetUtcNow();
				Publish(current=>current with
				{
					LastMeasurement=measurement,
					LastMeasurementAt=recordedAt,
					LastMeasurementTimestamp=timestamp,
					MeasurementAge=TimeSpan.Zero,
					Error=null
				});
				MeasurementReceived?.Invoke(
					this,
					new MeasurementSample(
						recordedAt,
						timestamp,
						measurement.VoltageHundredths,
						measurement.CurrentThousandths,
						timeProvider.GetElapsedTime(
							sessionStartedTimestamp,
							timestamp)));
				measurementRequest.Completion.TrySetResult();
				break;
			default:
				throw new InvalidOperationException(
					$"Nieznane żądanie sesji: {request.GetType().Name}.");
		}
	}

	private async Task PollMeasurementsAsync(CancellationToken cancellationToken)
	{
		while(true)
		{
			if(Snapshot.OutputState != OutputState.On)
			{
				await Task.Delay(
					pollingInterval,
					timeProvider,
					cancellationToken).ConfigureAwait(false);
				continue;
			}

			long cycleStarted=timeProvider.GetTimestamp();
			MeasurementRequest request=requests.RequestMeasurement();
			await request.Completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
			TimeSpan remaining=pollingInterval-timeProvider.GetElapsedTime(
				cycleStarted,
				timeProvider.GetTimestamp());
			if(remaining > TimeSpan.Zero)
			{
				await Task.Delay(
					remaining,
					timeProvider,
					cancellationToken).ConfigureAwait(false);
			}
		}
	}

	private void EnsureRunning()
	{
		ObjectDisposedException.ThrowIf(disposed,this);
		lock(lifecycleGate)
		{
			if(runTask is null || runTask.IsCompleted)
			{
				throw new InvalidOperationException("Sesja nie jest uruchomiona.");
			}
		}
	}

	private void Publish(Func<SessionSnapshot,SessionSnapshot> update)
	{
		SessionSnapshot published;
		lock(snapshotGate)
		{
			snapshot=update(snapshot);
			published=snapshot;
		}

		SnapshotChanged?.Invoke(this,published);
	}
}
