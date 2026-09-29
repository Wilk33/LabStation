namespace Ka3005P.Core.Protocol;

public readonly record struct VoltageSetpoint
{
	public int Hundredths { get; }

	private VoltageSetpoint(int hundredths)
	{
		Hundredths=hundredths;
	}

	public static VoltageSetpoint FromHundredths(int value)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(value);
		if(value > 6200)
		{
			throw new ArgumentOutOfRangeException(nameof(value));
		}

		return new VoltageSetpoint(value);
	}
}
