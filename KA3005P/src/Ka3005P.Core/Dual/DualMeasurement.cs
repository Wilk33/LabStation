using Ka3005P.Core.Measurements;

namespace Ka3005P.Core.Dual;

public readonly record struct DualMeasurement(
	DateTimeOffset RecordedAt,
	long Timestamp,
	DualMode Mode,
	MeasurementSample First,
	MeasurementSample Second,
	int VoltageHundredths,
	int CurrentThousandths,
	int FirstSignedVoltageHundredths,
	int SecondSignedVoltageHundredths,
	int FirstSignedCurrentThousandths,
	int SecondSignedCurrentThousandths)
{
	public static DualMeasurement Aggregate(
		DualMode mode,
		MeasurementSample first,
		MeasurementSample second)
	{
		(int voltage,int current)=mode switch
		{
			DualMode.Series=>
				(first.VoltageHundredths+second.VoltageHundredths,
				Math.Max(first.CurrentThousandths,second.CurrentThousandths)),
			DualMode.Parallel=>
				(Math.Max(first.VoltageHundredths,second.VoltageHundredths),
				first.CurrentThousandths+second.CurrentThousandths),
			DualMode.Symmetric=>
				(Math.Max(first.VoltageHundredths,second.VoltageHundredths),
				Math.Max(first.CurrentThousandths,second.CurrentThousandths)),
			_=>throw new ArgumentOutOfRangeException(nameof(mode))
		};

		int firstVoltage=mode == DualMode.Symmetric
			? -first.VoltageHundredths
			: first.VoltageHundredths;
		int firstCurrent=mode == DualMode.Symmetric
			? -first.CurrentThousandths
			: first.CurrentThousandths;
		return new DualMeasurement(
			first.RecordedAt >= second.RecordedAt ? first.RecordedAt : second.RecordedAt,
			Math.Max(first.Timestamp,second.Timestamp),
			mode,
			first,
			second,
			voltage,
			current,
			firstVoltage,
			second.VoltageHundredths,
			firstCurrent,
			second.CurrentThousandths);
	}
}
