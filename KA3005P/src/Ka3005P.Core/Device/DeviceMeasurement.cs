namespace Ka3005P.Core.Device;

public readonly record struct DeviceMeasurement(
	int VoltageHundredths,
	int CurrentThousandths);
