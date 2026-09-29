using Ka3005P.Core.Device;
using Ka3005P.Core.Protocol;

namespace Ka3005P.Core.Sessions;

public sealed record SessionSnapshot
{
	public bool IsRunning { get; init; }
	public VoltageSetpoint? RequestedVoltage { get; init; }
	public VoltageSetpoint? SentVoltage { get; init; }
	public CurrentSetpoint? RequestedCurrent { get; init; }
	public CurrentSetpoint? SentCurrent { get; init; }
	public bool? RequestedOutput { get; init; }
	public OutputState OutputState { get; init; }=OutputState.Unknown;
	public DeviceMeasurement? LastMeasurement { get; init; }
	public DateTimeOffset? LastMeasurementAt { get; init; }
	public long? LastMeasurementTimestamp { get; init; }
	public TimeSpan? MeasurementAge { get; init; }
	public SessionError? Error { get; init; }
}
