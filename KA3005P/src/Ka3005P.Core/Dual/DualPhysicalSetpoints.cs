using Ka3005P.Core.Protocol;

namespace Ka3005P.Core.Dual;

public readonly record struct DualPhysicalSetpoints(
	VoltageSetpoint FirstVoltage,
	VoltageSetpoint SecondVoltage,
	CurrentSetpoint FirstCurrent,
	CurrentSetpoint SecondCurrent);
