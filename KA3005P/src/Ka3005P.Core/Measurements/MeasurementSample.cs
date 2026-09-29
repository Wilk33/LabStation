namespace Ka3005P.Core.Measurements;

public readonly record struct MeasurementSample(
	DateTimeOffset RecordedAt,
	long Timestamp,
	int VoltageHundredths,
	int CurrentThousandths,
	TimeSpan Elapsed)
{
	public MeasurementSample(
		DateTimeOffset recordedAt,
		long timestamp,
		int voltageHundredths,
		int currentThousandths)
		: this(
			recordedAt,
			timestamp,
			voltageHundredths,
			currentThousandths,
			TimeSpan.Zero)
	{
	}

	public MeasurementSample(
		TimeSpan elapsed,
		int voltageHundredths,
		int currentThousandths)
		: this(
			DateTimeOffset.UnixEpoch+elapsed,
			elapsed.Ticks,
			voltageHundredths,
			currentThousandths,
			elapsed)
	{
	}
}
