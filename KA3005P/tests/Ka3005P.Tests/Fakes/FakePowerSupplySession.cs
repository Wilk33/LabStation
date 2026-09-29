using Ka3005P.Core.Device;
using Ka3005P.Core.Measurements;
using Ka3005P.Core.Protocol;
using Ka3005P.Core.Sessions;

namespace Ka3005P.Tests.Fakes;

internal sealed class FakePowerSupplySession : IPowerSupplySession
{
	private readonly List<int> voltages=[];
	private readonly List<int> currents=[];
	private readonly List<bool> outputs=[];
	private TaskCompletionSource outputStarted=NewSignal();
	private TaskCompletionSource outputRelease=NewSignal();
	private bool blockNextOutput;
	private int measurementRequests;

	public event EventHandler<SessionSnapshot>? SnapshotChanged;
	public event EventHandler<MeasurementSample>? MeasurementReceived;

	public SessionSnapshot Snapshot { get; private set; }=new();
	public int PendingSetpointCount => 0;
	public IReadOnlyList<int> Voltages => voltages;
	public IReadOnlyList<int> Currents => currents;
	public IReadOnlyList<bool> Outputs => outputs;
	public bool OutputRequested => outputs.Count > 0;
	public VoltageSetpoint? RequestedVoltage =>
		voltages.Count == 0 ? null : VoltageSetpoint.FromHundredths(voltages[^1]);
	public CurrentSetpoint? RequestedCurrent =>
		currents.Count == 0 ? null : CurrentSetpoint.FromThousandths(currents[^1]);
	public bool StopRequested { get; private set; }
	public bool DisposeRequested { get; private set; }
	public int MeasurementRequests => measurementRequests;
	public Exception? OutputFailure { get; set; }

	public void BlockNextOutput()
	{
		blockNextOutput=true;
		outputStarted=NewSignal();
		outputRelease=NewSignal();
	}

	public Task WaitUntilOutputStartsAsync()
	{
		return outputStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
	}

	public void ReleaseOutput()
	{
		outputRelease.TrySetResult();
	}

	public ValueTask StartAsync(CancellationToken cancellationToken)
	{
		Snapshot=Snapshot with { IsRunning=true };
		SnapshotChanged?.Invoke(this,Snapshot);
		return ValueTask.CompletedTask;
	}

	public ValueTask StopAsync(CancellationToken cancellationToken)
	{
		StopRequested=true;
		Snapshot=Snapshot with { IsRunning=false };
		SnapshotChanged?.Invoke(this,Snapshot);
		return ValueTask.CompletedTask;
	}

	public void RequestVoltage(VoltageSetpoint value)
	{
		voltages.Add(value.Hundredths);
		Snapshot=Snapshot with
		{
			RequestedVoltage=value,
			SentVoltage=value
		};
		SnapshotChanged?.Invoke(this,Snapshot);
	}

	public void RequestCurrent(CurrentSetpoint value)
	{
		currents.Add(value.Thousandths);
		Snapshot=Snapshot with
		{
			RequestedCurrent=value,
			SentCurrent=value
		};
		SnapshotChanged?.Invoke(this,Snapshot);
	}

	public void RequestMeasurement()
	{
		measurementRequests++;
	}

	public async ValueTask SetOutputAsync(
		bool enabled,
		CancellationToken cancellationToken)
	{
		outputs.Add(enabled);
		if(blockNextOutput)
		{
			blockNextOutput=false;
			outputStarted.TrySetResult();
			await outputRelease.Task.WaitAsync(cancellationToken);
		}
		if(OutputFailure is not null)
		{
			throw OutputFailure;
		}

		Snapshot=Snapshot with
		{
			RequestedOutput=enabled,
			OutputState=enabled ? OutputState.On : OutputState.Off
		};
		SnapshotChanged?.Invoke(this,Snapshot);
	}

	public void PublishMeasurement(MeasurementSample sample)
	{
		Snapshot=Snapshot with
		{
			LastMeasurement=new DeviceMeasurement(
				sample.VoltageHundredths,
				sample.CurrentThousandths)
		};
		SnapshotChanged?.Invoke(this,Snapshot);
		MeasurementReceived?.Invoke(this,sample);
	}

	public void PublishError(Exception exception)
	{
		Snapshot=Snapshot with
		{
			Error=new SessionError(
				DateTimeOffset.UtcNow,
				exception.Message,
				exception)
		};
		SnapshotChanged?.Invoke(this,Snapshot);
	}

	public ValueTask DisposeAsync()
	{
		DisposeRequested=true;
		return ValueTask.CompletedTask;
	}

	private static TaskCompletionSource NewSignal()
	{
		return new(TaskCreationOptions.RunContinuationsAsynchronously);
	}
}
