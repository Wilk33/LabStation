namespace Sdl1000X.Core;

public enum LoadMode
{
	ConstantCurrent,
	ConstantVoltage,
	ConstantPower,
	ConstantResistance,
	Led
}

public sealed record LoadModeProfile(
	string Name,
	string Unit,
	double Minimum,
	double Maximum,
	double Step,
	int DecimalPlaces);

public sealed record LoadMeasurements(
	double Voltage,
	double Current,
	double Power,
	double Resistance);

public sealed record LedSettings(
	double Voltage,
	double Current,
	double Resistance);

public sealed record ProtectionSettings(
	bool OverCurrentEnabled,
	double OverCurrentAmps,
	bool OverPowerEnabled,
	double OverPowerWatts);

public sealed record ElectronicLoadSnapshot(
	LoadMode Mode,
	bool InputEnabled,
	double? Setpoint,
	LedSettings? Led,
	ProtectionSettings Protections,
	LoadMeasurements Measurements,
	DateTimeOffset Timestamp);
