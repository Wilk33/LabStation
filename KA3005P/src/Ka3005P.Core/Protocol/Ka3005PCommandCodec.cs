using System.Globalization;
using System.Text;

namespace Ka3005P.Core.Protocol;

public static class Ka3005PCommandCodec
{
	private const int MaximumDeviceVoltageHundredths=3100;
	private const int MaximumDeviceCurrentThousandths=5100;

	public static byte[] SetVoltage(VoltageSetpoint value)
	{
		if(value.Hundredths > MaximumDeviceVoltageHundredths)
		{
			throw new ArgumentOutOfRangeException(nameof(value));
		}

		int whole=value.Hundredths/100;
		int fraction=value.Hundredths%100;
		return Encoding.ASCII.GetBytes(
			string.Create(CultureInfo.InvariantCulture,$"VSET1:{whole:00}.{fraction:00}"));
	}

	public static byte[] SetCurrent(CurrentSetpoint value)
	{
		if(value.Thousandths > MaximumDeviceCurrentThousandths)
		{
			throw new ArgumentOutOfRangeException(nameof(value));
		}

		int whole=value.Thousandths/1000;
		int fraction=value.Thousandths%1000;
		return Encoding.ASCII.GetBytes(
			string.Create(CultureInfo.InvariantCulture,$"ISET1:{whole:0}.{fraction:000}"));
	}

	public static byte[] SetOutput(bool enabled)
	{
		return Encoding.ASCII.GetBytes(enabled ? "OUT1" : "OUT0");
	}

	public static byte[] ReadVoltage()
	{
		return "VOUT1?"u8.ToArray();
	}

	public static byte[] ReadCurrent()
	{
		return "IOUT1?"u8.ToArray();
	}

	public static VoltageSetpoint ParseVoltage(ReadOnlySpan<byte> reply)
	{
		if(reply.Length != 5 || reply[2] != (byte)'.')
		{
			throw new ProtocolException("Odpowiedź napięcia musi mieć format dd.dd.");
		}

		int hundredths=ParseDigit(reply[0])*1000+
			ParseDigit(reply[1])*100+
			ParseDigit(reply[3])*10+
			ParseDigit(reply[4]);

		try
		{
			return VoltageSetpoint.FromHundredths(hundredths);
		}
		catch(ArgumentOutOfRangeException exception)
		{
			throw new ProtocolException("Odpowiedź napięcia jest poza zakresem.",exception);
		}
	}

	public static CurrentSetpoint ParseCurrent(ReadOnlySpan<byte> reply)
	{
		if(reply.Length != 5 || reply[1] != (byte)'.')
		{
			throw new ProtocolException("Odpowiedź prądu musi mieć format d.ddd.");
		}

		int thousandths=ParseDigit(reply[0])*1000+
			ParseDigit(reply[2])*100+
			ParseDigit(reply[3])*10+
			ParseDigit(reply[4]);

		return CurrentSetpoint.FromThousandths(thousandths);
	}

	private static int ParseDigit(byte value)
	{
		if(value < (byte)'0' || value > (byte)'9')
		{
			throw new ProtocolException("Odpowiedź zawiera znak, który nie jest cyfrą.");
		}

		return value-(byte)'0';
	}
}
