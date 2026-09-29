using Ka3005P.Core.Protocol;
using Ka3005P.Core.Measurements;

namespace Ka3005P.Core.Sessions;

public interface IPowerSupplySession : IAsyncDisposable
{
	event EventHandler<SessionSnapshot>? SnapshotChanged;
	event EventHandler<MeasurementSample>? MeasurementReceived;

	SessionSnapshot Snapshot { get; }
	int PendingSetpointCount { get; }

	ValueTask StartAsync(CancellationToken cancellationToken);
	ValueTask StopAsync(CancellationToken cancellationToken);
	void RequestVoltage(VoltageSetpoint value);
	void RequestCurrent(CurrentSetpoint value);
	void RequestMeasurement();
	ValueTask SetOutputAsync(bool enabled,CancellationToken cancellationToken);
}
