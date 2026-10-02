namespace Sdm3000.Core;

public enum MeasurementFunction
{
	Unknown,
	VoltageDc,
	VoltageAc,
	CurrentDc,
	CurrentAc,
	Resistance2Wire,
	Resistance4Wire,
	Capacitance,
	Continuity,
	Diode,
	Frequency,
	Period,
	Temperature
}

public enum ReadingState
{
	Value,
	Overload
}

public sealed record MeasurementConfiguration(
	MeasurementFunction Function,
	double? Range);

public sealed record MeasurementReading(
	ReadingState State,
	double? Value);

public sealed record MeasurementProfile(
	string Name,
	string ShortName,
	string PrimaryLabel,
	string Unit);

public sealed record LocalStatisticsSnapshot(
	long Count,
	double? Minimum,
	double? Maximum,
	double? Average,
	double? PeakToPeak,
	double? StandardDeviation);

public sealed record MeasurementSnapshot(
	MeasurementConfiguration Configuration,
	MeasurementReading Reading,
	long StoredPoints,
	LocalStatisticsSnapshot Statistics,
	DateTimeOffset Timestamp);
