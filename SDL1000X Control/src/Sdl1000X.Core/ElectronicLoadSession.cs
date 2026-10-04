using LabStation.Instruments.Scpi;
using LabStation.Instruments.Scheduling;
using LabStation.Instruments.Transport;

namespace Sdl1000X.Core;

public sealed class ElectronicLoadSession : IAsyncDisposable
{
	private readonly SiglentElectronicLoadClient client;
	private readonly LatestRequestQueue<LoadWrite> requests=new();
	private readonly SemaphoreSlim ioGate=new(1,1);
	private readonly CancellationTokenSource cancellation=new();
	private readonly Task worker;
	private bool disposed;

	private ElectronicLoadSession(
		SiglentElectronicLoadClient client,
		ScpiIdentity identity)
	{
		this.client=client;
		Identity=identity;
		worker=Task.Run(ProcessWritesAsync);
	}

	public event EventHandler<Exception>? CommunicationFailed;
	public ScpiIdentity Identity { get; }

	public static async Task<ElectronicLoadSession> ConnectAsync(string host)
	{
		return await CreateAsync(()=>new Vxi11Transport(host));
	}

	public static async Task<ElectronicLoadSession> CreateAsync(
		Func<IInstrumentTransport> transportFactory)
	{
		ArgumentNullException.ThrowIfNull(transportFactory);
		return await Task.Run(() =>
		{
			IInstrumentTransport transport=transportFactory();
			SiglentElectronicLoadClient client=new(transport);
			try
			{
				ScpiIdentity identity=client.Initialize();
				return new ElectronicLoadSession(client,identity);
			}
			catch
			{
				client.Dispose();
				throw;
			}
		});
	}

	public async Task<ElectronicLoadSnapshot> ReadAsync()
	{
		ObjectDisposedException.ThrowIf(disposed,this);
		await ioGate.WaitAsync(cancellation.Token);
		try
		{
			return await Task.Run(client.ReadSnapshot,cancellation.Token);
		}
		finally
		{
			ioGate.Release();
		}
	}

	public Task SetModeAsync(LoadMode mode)
	{
		ObjectDisposedException.ThrowIf(disposed,this);
		return requests.EnqueueLatest(
			"MODE",
			new(load=>load.SetMode(mode))).Completion;
	}

	public Task SetSetpointAsync(LoadMode mode,double value)
	{
		ObjectDisposedException.ThrowIf(disposed,this);
		ElectronicLoadProtocol.ValidateSetpoint(mode,value);
		return requests.EnqueueLatest(
			"SETPOINT:"+mode,
			new(load=>load.SetSetpoint(mode,value))).Completion;
	}

	public Task SetLedSettingsAsync(LedSettings settings)
	{
		ObjectDisposedException.ThrowIf(disposed,this);
		ElectronicLoadProtocol.ValidateLed(settings);
		return requests.EnqueueLatest(
			"LED",
			new(load=>load.SetLedSettings(settings))).Completion;
	}

	public Task SetProtectionsAsync(ProtectionSettings settings)
	{
		ObjectDisposedException.ThrowIf(disposed,this);
		ElectronicLoadProtocol.OverCurrentLevelCommand(settings.OverCurrentAmps);
		ElectronicLoadProtocol.OverPowerLevelCommand(settings.OverPowerWatts);
		return requests.EnqueueLatest(
			"PROTECTION",
			new(load=>load.SetProtections(settings))).Completion;
	}

	public Task SetInputAsync(bool enabled)
	{
		ObjectDisposedException.ThrowIf(disposed,this);
		ScheduledRequest<LoadWrite> request=enabled
			? requests.EnqueueOrdered(new(load=>load.SetInput(true)))
			: requests.EnqueuePriority(new(load=>load.SetInput(false)));
		return request.Completion;
	}

	public async ValueTask DisposeAsync()
	{
		if(disposed)
		{
			return;
		}
		disposed=true;
		requests.CancelAll();
		cancellation.Cancel();
		try
		{
			await worker;
		}
		catch(OperationCanceledException)
		{
		}
		await ioGate.WaitAsync();
		Exception? shutdownError=null;
		try
		{
			try
			{
				await Task.Run(()=>client.SetInput(false));
			}
			catch(Exception exception)
			{
				shutdownError=exception;
			}
			client.Dispose();
		}
		finally
		{
			ioGate.Release();
			ioGate.Dispose();
			requests.Dispose();
			cancellation.Dispose();
		}
		if(shutdownError is not null)
		{
			throw new IOException(
				"Nie udało się potwierdzić wyłączenia wejścia podczas rozłączania.",
				shutdownError);
		}
	}

	private async Task ProcessWritesAsync()
	{
		while(!cancellation.IsCancellationRequested)
		{
			await requests.WaitAsync(cancellation.Token);
			while(requests.TryTakeNext(out ScheduledRequest<LoadWrite>? request))
			{
				try
				{
					await ioGate.WaitAsync(cancellation.Token);
					try
					{
						await Task.Run(
							()=>request.Value.Execute(client),
							cancellation.Token);
					}
					finally
					{
						ioGate.Release();
					}
					request.Succeed();
				}
				catch(OperationCanceledException)
				{
					request.Cancel();
					throw;
				}
				catch(Exception exception)
				{
					request.Fail(exception);
					CommunicationFailed?.Invoke(this,exception);
				}
			}
		}
	}

	private sealed record LoadWrite(
		Action<SiglentElectronicLoadClient> Execute);
}
