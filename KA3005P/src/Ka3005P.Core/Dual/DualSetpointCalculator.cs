using Ka3005P.Core.Protocol;

namespace Ka3005P.Core.Dual;

public static class DualSetpointCalculator
{
	public static DualPhysicalSetpoints Calculate(
		DualMode mode,
		VoltageSetpoint voltage,
		CurrentSetpoint current)
	{
		return mode switch
		{
			DualMode.Series=>CalculateSeries(voltage,current),
			DualMode.Parallel=>CalculateParallel(voltage,current),
			DualMode.Symmetric=>CalculateSymmetric(voltage,current),
			_=>throw new ArgumentOutOfRangeException(nameof(mode))
		};
	}

	private static DualPhysicalSetpoints CalculateSeries(
		VoltageSetpoint voltage,
		CurrentSetpoint current)
	{
		if(current.Thousandths > 5100)
		{
			throw new ArgumentOutOfRangeException(nameof(current));
		}

		(int first,int second)=Split(voltage.Hundredths);
		return new DualPhysicalSetpoints(
			VoltageSetpoint.FromHundredths(first),
			VoltageSetpoint.FromHundredths(second),
			current,
			current);
	}

	private static DualPhysicalSetpoints CalculateParallel(
		VoltageSetpoint voltage,
		CurrentSetpoint current)
	{
		if(voltage.Hundredths > 3100)
		{
			throw new ArgumentOutOfRangeException(nameof(voltage));
		}

		(int first,int second)=Split(current.Thousandths);
		return new DualPhysicalSetpoints(
			voltage,
			voltage,
			CurrentSetpoint.FromThousandths(first),
			CurrentSetpoint.FromThousandths(second));
	}

	private static DualPhysicalSetpoints CalculateSymmetric(
		VoltageSetpoint voltage,
		CurrentSetpoint current)
	{
		if(voltage.Hundredths > 3100)
		{
			throw new ArgumentOutOfRangeException(nameof(voltage));
		}
		if(current.Thousandths > 5100)
		{
			throw new ArgumentOutOfRangeException(nameof(current));
		}

		return new DualPhysicalSetpoints(voltage,voltage,current,current);
	}

	private static (int First,int Second) Split(int total)
	{
		int first=total/2;
		return (first,total-first);
	}
}
