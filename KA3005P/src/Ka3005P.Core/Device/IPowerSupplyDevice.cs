using Ka3005P.Core.Protocol;

namespace Ka3005P.Core.Device;

public interface IPowerSupplyDevice : IAsyncDisposable
{
	ValueTask SetVoltageAsync(VoltageSetpoint value,CancellationToken cancellationToken);
	ValueTask SetCurrentAsync(CurrentSetpoint value,CancellationToken cancellationToken);
	ValueTask SetOutputAsync(bool enabled,CancellationToken cancellationToken);
	ValueTask<DeviceMeasurement> ReadMeasurementAsync(CancellationToken cancellationToken);
}
