using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Scope.Core;

public sealed record Waveform(int Channel, double[] Volts, double Interval, double Start, DateTimeOffset CapturedAt)

{

	public static Waveform Decode(int channel, byte[] message)

	{

		byte[] b=Scpi.Block(message);
		if (b.Length < 346 || Encoding.ASCII.GetString(b, 0, 8) != "WAVEDESC")
			throw new InvalidDataException("Brak deskryptora WAVEDESC. Zachowaj plik diagnostyczny i sprawdź firmware CML+.");
		bool little=b[34] == 1 && b[35] == 0;
		int I16(int p) => little ? BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(p)) : BinaryPrimitives.ReadInt16BigEndian(b.AsSpan(p));
		int I32(int p) => little ? BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(p)) : BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(p));
		float F32(int p) => BitConverter.Int32BitsToSingle(I32(p));
		double F64(int p) => BitConverter.Int64BitsToDouble(little ? BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(p)) : BinaryPrimitives.ReadInt64BigEndian(b.AsSpan(p)));
		if (I16(34) != 0 && I16(34) != 1)
			throw new InvalidDataException("Nieznana kolejność bajtów.");
		int width=I16(32) switch

		{

			0 => 1,
			1 => 2,
			_ => throw new InvalidDataException("Nieznany format próbek.")

		};
		int descriptor=I32(36);
		long offset=descriptor;
		if (descriptor < 346)
			throw new InvalidDataException("Niepełny deskryptor.");
		foreach (int p in new[]
{
40, 44, 48, 52, 56
})

		{

			int length=I32(p);
			if (length < 0)
				throw new InvalidDataException("Ujemna długość sekcji.");
			offset+=length;

		}

		int bytes=I32(60);
		int count=I32(116);
		if (bytes <= 0 || bytes%width != 0 || offset+bytes > b.Length || offset > int.MaxValue || count != bytes/width)
			throw new InvalidDataException("Niepełny przebieg lub sprzeczna liczba próbek.");
		double gain=F32(156), shift=F32(160), interval=F32(176), start=F64(180);
		if (!double.IsFinite(gain) || gain <= 0 || !double.IsFinite(shift) || !double.IsFinite(interval) || interval <= 0 || !double.IsFinite(start))
			throw new InvalidDataException("Nieprawidłowe skalowanie w deskryptorze.");
		double[] volts=new double[count];
		for (int i=0; i < count; i++)

		{

			int p=(int)offset+i*width;
			int code=width == 1 ? (sbyte)b[p] : I16(p);
			volts[i]=code*gain-shift;

		}

		return new(channel, volts, interval, start, DateTimeOffset.UtcNow);

	}


}


public static partial class Scpi

{

	public const int MaxMessage=32*1024*1024;
	public static byte[] Block(byte[] message)

	{

		int hash=Array.IndexOf(message, (byte)'#', 0, Math.Min(message.Length, 128));
		if (hash < 0 || hash+2 > message.Length)
			throw new InvalidDataException("Brak nagłówka bloku binarnego.");
		int digits=message[hash+1]-'0';
		if (digits < 1 || digits > 9 || hash+2+digits > message.Length)
			throw new InvalidDataException("Nieprawidłowy nagłówek binarny.");
		if (!int.TryParse(Encoding.ASCII.GetString(message, hash+2, digits), NumberStyles.None, CultureInfo.InvariantCulture, out int length) || length <= 0 || length > MaxMessage)
			throw new InvalidDataException("Nieprawidłowa długość bloku.");
		int start=hash+2+digits;
		if (message.Length-start < length)
			throw new InvalidDataException("Transfer przebiegu został przerwany.");
		return message.AsSpan(start, length).ToArray();

	}

	public static double Number(string response)

	{

		string value=response.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1];
		Match m=ValuePattern().Match(value);
		if (!m.Success)
			throw new InvalidDataException("Nieprawidłowa odpowiedź liczbowa: "+response);
		double n=double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
		double scale=m.Groups[2].Value switch

		{

			"G" => 1e9,
			"M" => 1e6,
			"K" or "k" => 1e3,
			"m" => 1e-3,
			"u" or "µ" => 1e-6,
			"n" => 1e-9,
			"p" => 1e-12,
			_ => 1

		};
		if (!double.IsFinite(n*scale))
			throw new InvalidDataException("Nieprawidłowa wartość.");
		return n*scale;

	}

	[GeneratedRegex(@"^([+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[Ee][+-]?\d+)?)([GMKkmunpµ]?)[A-Za-z/]*$")]
	private static partial Regex ValuePattern();

}


public static class Csv

{

	public static void Write(TextWriter writer, IEnumerable<Waveform> waveforms)

	{

		writer.WriteLine("channel,time_s,voltage_V,captured_utc");
		foreach (Waveform w in waveforms)

		{

			for (int i=0; i < w.Volts.Length; i++)

			{

				writer.Write("CH");
				writer.Write(w.Channel);
				writer.Write(',');
				writer.Write((w.Start+i*w.Interval).ToString("G17", CultureInfo.InvariantCulture));
				writer.Write(',');
				writer.Write(w.Volts[i].ToString("G17", CultureInfo.InvariantCulture));
				writer.Write(',');
				writer.WriteLine(w.CapturedAt.ToString("O", CultureInfo.InvariantCulture));

			}


		}


	}


}
