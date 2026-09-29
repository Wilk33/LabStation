namespace Ka3005P.Core.Protocol;

public readonly record struct CurrentSetpoint
{
	public int Thousandths { get; }

	private CurrentSetpoint(int thousandths)
	{
		Thousandths=thousandths;
	}

	public static CurrentSetpoint FromThousandths(int value)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(value);
		if(value > 10200)
		{
			throw new ArgumentOutOfRangeException(nameof(value));
		}

		return new CurrentSetpoint(value);
	}
}
