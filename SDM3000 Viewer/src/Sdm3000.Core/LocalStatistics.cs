namespace Sdm3000.Core;

public sealed class LocalStatistics
{
	private long count;
	private double mean;
	private double sumSquaredDifferences;
	private double minimum=double.PositiveInfinity;
	private double maximum=double.NegativeInfinity;

	public void Add(MeasurementReading reading)
	{
		if(reading.State != ReadingState.Value || reading.Value is not double value)
		{
			return;
		}
		count++;
		double delta=value-mean;
		mean+=delta/count;
		double deltaAfter=value-mean;
		sumSquaredDifferences+=delta*deltaAfter;
		minimum=Math.Min(minimum,value);
		maximum=Math.Max(maximum,value);
	}

	public LocalStatisticsSnapshot Snapshot()
	{
		if(count == 0)
		{
			return new(0,null,null,null,null,null);
		}
		return new(
			count,
			minimum,
			maximum,
			mean,
			maximum-minimum,
			Math.Sqrt(sumSquaredDifferences/count));
	}
}

public sealed class MeasurementAccumulator
{
	private MeasurementFunction function=MeasurementFunction.Unknown;
	private LocalStatistics statistics=new();

	public MeasurementSnapshot Accept(
		MeasurementConfiguration configuration,
		MeasurementReading reading,
		long storedPoints)
	{
		if(configuration.Function != function)
		{
			function=configuration.Function;
			statistics=new LocalStatistics();
		}
		statistics.Add(reading);
		return new(
			configuration,
			reading,
			storedPoints,
			statistics.Snapshot(),
			DateTimeOffset.Now);
	}
}
