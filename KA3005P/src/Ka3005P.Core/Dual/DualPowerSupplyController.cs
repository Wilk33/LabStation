using Ka3005P.Core.Measurements;
using Ka3005P.Core.Protocol;
using Ka3005P.Core.Sessions;

namespace Ka3005P.Core.Dual;

public sealed record DualControllerSnapshot(
	DualMode Mode,
	VoltageSetpoint RequestedVoltage,
	CurrentSetpoint RequestedCurrent,
	DualPhysicalSetpoints PhysicalSetpoints,
	SessionSnapshot First,
	SessionSnapshot Second,
	DualMeasurement? LastMeasurement,
	DualOperationResult? LastOutputOperation);

public sealed class DualPowerSupplyController : IAsyncDisposable
{
	private readonly IPowerSupplySession first;
	private readonly IPowerSupplySession second;
	private readonly object gate=new();
	private VoltageSetpoint requestedVoltage=VoltageSetpoint.FromHundredths(0);
	private CurrentSetpoint requestedCurrent=CurrentSetpoint.FromThousandths(0);
	private MeasurementSample? firstMeasurement;
	private MeasurementSample? secondMeasurement;
	private bool firstMeasurementUpdated;
	private bool secondMeasurementUpdated;
	private DualControllerSnapshot snapshot;
	private bool disposed;

	public DualPowerSupplyController(
		IPowerSupplySession first,
		IPowerSupplySession second,
		DualMode mode=DualMode.Series)
	{
		ArgumentNullException.ThrowIfNull(first);
		ArgumentNullException.ThrowIfNull(second);
		this.first=first;
		this.second=second;
		Mode=mode;
		DualPhysicalSetpoints physical=DualSetpointCalculator.Calculate(
			mode,
			requestedVoltage,
			requestedCurrent);
		snapshot=new DualControllerSnapshot(
			mode,
			requestedVoltage,
			requestedCurrent,
			physical,
			first.Snapshot,
			second.Snapshot,
			null,
			null);
		first.SnapshotChanged+=OnSessionSnapshotChanged;
		second.SnapshotChanged+=OnSessionSnapshotChanged;
		first.MeasurementReceived+=OnFirstMeasurement;
		second.MeasurementReceived+=OnSecondMeasurement;
	}

	public event EventHandler<DualControllerSnapshot>? DualSnapshotChanged;
	public event EventHandler<DualMeasurement>? MeasurementReceived;

	public DualMode Mode { get; private set; }

	public DualControllerSnapshot Snapshot
	{
		get
		{
			lock(gate)
			{
				return snapshot;
			}
		}
	}

	public void SetMode(DualMode mode)
	{
		ThrowIfDisposed();
		DualPhysicalSetpoints physical=DualSetpointCalculator.Calculate(
			mode,
			requestedVoltage,
			requestedCurrent);
		Mode=mode;
		first.RequestVoltage(physical.FirstVoltage);
		second.RequestVoltage(physical.SecondVoltage);
		first.RequestCurrent(physical.FirstCurrent);
		second.RequestCurrent(physical.SecondCurrent);
		UpdateSnapshot(current=>current with
		{
			Mode=mode,
			PhysicalSetpoints=physical
		});
	}

	public void RequestVoltage(VoltageSetpoint value)
	{
		ThrowIfDisposed();
		DualPhysicalSetpoints physical=DualSetpointCalculator.Calculate(
			Mode,
			value,
			requestedCurrent);
		requestedVoltage=value;
		first.RequestVoltage(physical.FirstVoltage);
		second.RequestVoltage(physical.SecondVoltage);
		UpdateSnapshot(current=>current with
		{
			RequestedVoltage=value,
			PhysicalSetpoints=physical
		});
	}

	public void RequestCurrent(CurrentSetpoint value)
	{
		ThrowIfDisposed();
		DualPhysicalSetpoints physical=DualSetpointCalculator.Calculate(
			Mode,
			requestedVoltage,
			value);
		requestedCurrent=value;
		first.RequestCurrent(physical.FirstCurrent);
		second.RequestCurrent(physical.SecondCurrent);
		UpdateSnapshot(current=>current with
		{
			RequestedCurrent=value,
			PhysicalSetpoints=physical
		});
	}

	public async ValueTask<DualOperationResult> SetOutputAsync(
		bool enabled,
		CancellationToken cancellationToken)
	{
		ThrowIfDisposed();
		Task<Exception?> firstTask=ExecuteOutputAsync(first,enabled,cancellationToken);
		Task<Exception?> secondTask=ExecuteOutputAsync(second,enabled,cancellationToken);
		await Task.WhenAll(firstTask,secondTask).ConfigureAwait(false);
		Exception? firstError=await firstTask.ConfigureAwait(false);
		Exception? secondError=await secondTask.ConfigureAwait(false);
		Exception? firstSafetyError=null;
		Exception? secondSafetyError=null;
		bool safetyOff=enabled && (firstError is not null || secondError is not null);
		if(safetyOff)
		{
			Task<Exception?> firstOff=ExecuteOutputAsync(first,false,CancellationToken.None);
			Task<Exception?> secondOff=ExecuteOutputAsync(second,false,CancellationToken.None);
			await Task.WhenAll(firstOff,secondOff).ConfigureAwait(false);
			firstSafetyError=await firstOff.ConfigureAwait(false);
			secondSafetyError=await secondOff.ConfigureAwait(false);
		}

		DualOperationResult result=new(
			Guid.NewGuid(),
			enabled,
			firstError,
			secondError,
			safetyOff,
			firstSafetyError,
			secondSafetyError);
		UpdateSnapshot(current=>current with { LastOutputOperation=result });
		return result;
	}

	public async ValueTask DisposeAsync()
	{
		if(disposed)
		{
			return;
		}

		disposed=true;
		first.SnapshotChanged-=OnSessionSnapshotChanged;
		second.SnapshotChanged-=OnSessionSnapshotChanged;
		first.MeasurementReceived-=OnFirstMeasurement;
		second.MeasurementReceived-=OnSecondMeasurement;
		await Task.WhenAll(
			first.DisposeAsync().AsTask(),
			second.DisposeAsync().AsTask()).ConfigureAwait(false);
	}

	private static async Task<Exception?> ExecuteOutputAsync(
		IPowerSupplySession session,
		bool enabled,
		CancellationToken cancellationToken)
	{
		try
		{
			await session.SetOutputAsync(enabled,cancellationToken).ConfigureAwait(false);
			return null;
		}
		catch(Exception exception)
		{
			return exception;
		}
	}

	private void OnSessionSnapshotChanged(object? sender,SessionSnapshot value)
	{
		UpdateSnapshot(current=>current with
		{
			First=first.Snapshot,
			Second=second.Snapshot
		});
	}

	private void OnFirstMeasurement(object? sender,MeasurementSample sample)
	{
		lock(gate)
		{
			firstMeasurement=sample;
			firstMeasurementUpdated=true;
		}
		PublishCombinedMeasurement();
	}

	private void OnSecondMeasurement(object? sender,MeasurementSample sample)
	{
		lock(gate)
		{
			secondMeasurement=sample;
			secondMeasurementUpdated=true;
		}
		PublishCombinedMeasurement();
	}

	private void PublishCombinedMeasurement()
	{
		DualMeasurement measurement;
		lock(gate)
		{
			if(!firstMeasurementUpdated ||
				!secondMeasurementUpdated ||
				firstMeasurement is not MeasurementSample firstSample ||
				secondMeasurement is not MeasurementSample secondSample)
			{
				return;
			}

			measurement=DualMeasurement.Aggregate(Mode,firstSample,secondSample);
			firstMeasurementUpdated=false;
			secondMeasurementUpdated=false;
			snapshot=snapshot with { LastMeasurement=measurement };
		}

		DualSnapshotChanged?.Invoke(this,Snapshot);
		MeasurementReceived?.Invoke(this,measurement);
	}

	private void UpdateSnapshot(
		Func<DualControllerSnapshot,DualControllerSnapshot> update)
	{
		DualControllerSnapshot published;
		lock(gate)
		{
			snapshot=update(snapshot);
			published=snapshot;
		}
		DualSnapshotChanged?.Invoke(this,published);
	}

	private void ThrowIfDisposed()
	{
		ObjectDisposedException.ThrowIf(disposed,this);
	}
}
