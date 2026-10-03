using System.Buffers.Binary;
using System.Globalization;

namespace Sdg1032X.Core;

public enum BasicWaveform
{
	Sine,
	Square,
	Ramp,
	Pulse,
	Noise,
	Dc,
	Arbitrary
}

public enum GeneratorParameter
{
	Frequency,
	Amplitude,
	Offset,
	Phase,
	Duty,
	Symmetry,
	PulseWidth,
	RiseTime,
	Delay,
	NoiseStandardDeviation,
	NoiseMean
}

public enum OutputLoad
{
	HighImpedance,
	Ohms50,
	Custom
}

public enum OutputPolarity
{
	Normal,
	Inverted
}

public sealed record ChannelSnapshot
{
	public required int Channel { get; init; }
	public required BasicWaveform Waveform { get; init; }
	public double FrequencyHz { get; init; }
	public double AmplitudeVpp { get; init; }
	public double OffsetVolts { get; init; }
	public double PhaseDegrees { get; init; }
	public double DutyPercent { get; init; }
	public double SymmetryPercent { get; init; }
	public double PulseWidthSeconds { get; init; }
	public double RiseTimeSeconds { get; init; }
	public double DelaySeconds { get; init; }
	public double NoiseStandardDeviation { get; init; }
	public double NoiseMean { get; init; }
	public bool OutputEnabled { get; init; }
	public required OutputLoad Load { get; init; }
	public double? LoadOhms { get; init; }
	public required OutputPolarity Polarity { get; init; }
}

public sealed record ArbitraryWaveformData
{
	private const int MaximumBytes=16384*2;

	private ArbitraryWaveformData(string name,byte[] data)
	{
		Name=name;
		Data=data;
	}

	public string Name { get; }
	public byte[] Data { get; }
	public int SampleCount=>Data.Length/2;

	public static ArbitraryWaveformData FromFile(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		return Path.GetExtension(path).ToLowerInvariant() switch
		{
			".bin"=>FromBytes(Path.GetFileName(path),File.ReadAllBytes(path)),
			".csv"=>FromEasyWaveCsv(path),
			_=>throw new InvalidDataException(
				"Obsługiwane są pliki EasyWave CSV i binarne pliki SIGLENT BIN.")
		};
	}

	private static ArbitraryWaveformData FromEasyWaveCsv(string path)
	{
		string[][] rows=File.ReadLines(path)
			.Select(line=>line.Split(','))
			.ToArray();
		if(rows.Length<15)
		{
			throw new InvalidDataException("Plik EasyWave CSV jest niekompletny.");
		}
		Dictionary<string,string> metadata=rows
			.Take(13)
			.Where(row=>row.Length>=2 && !string.IsNullOrWhiteSpace(row[0]))
			.ToDictionary(
				row=>row[0].Trim().ToLowerInvariant(),
				row=>row[1].Trim(),
				StringComparer.OrdinalIgnoreCase);
		int declaredLength=ParseInteger(metadata,"data length");
		double amplitudeVpp=ParseNumber(metadata,"amp");
		double offsetVolts=ParseNumber(metadata,"offset");
		_ = ParseNumber(metadata,"frequency");
		_ = ParseNumber(metadata,"phase");
		if(declaredLength is < 2 or > 16384)
		{
			throw new InvalidDataException(
				"EasyWave CSV musi zawierać od 2 do 16 384 próbek.");
		}
		if(!double.IsFinite(amplitudeVpp) || amplitudeVpp<=0)
		{
			throw new InvalidDataException("Amplituda w EasyWave CSV musi być dodatnia.");
		}
		int headerIndex=Array.FindIndex(rows,row=>
			row.Length>=2 &&
			row[0].Trim().Equals("xpos",StringComparison.OrdinalIgnoreCase) &&
			row[1].Trim().Equals("value",StringComparison.OrdinalIgnoreCase));
		if(headerIndex<0)
		{
			throw new InvalidDataException("Brak kolumn xpos,value w pliku EasyWave CSV.");
		}
		double[] samples=rows
			.Skip(headerIndex+1)
			.Where(row=>row.Length>=2 && !string.IsNullOrWhiteSpace(row[1]))
			.Select(row=>ParseNumber(row[1],"próbka"))
			.ToArray();
		if(samples.Length != declaredLength)
		{
			throw new InvalidDataException(
				$"EasyWave CSV deklaruje {declaredLength} próbek, a zawiera {samples.Length}.");
		}
		byte[] data=new byte[samples.Length*2];
		double halfAmplitude=amplitudeVpp/2;
		for(int index=0;index<samples.Length;index++)
		{
			double normalized=Math.Clamp(
				(samples[index]-offsetVolts)/halfAmplitude,
				-1,
				1);
			int code=normalized<0
				? (int)Math.Round(normalized*8192,MidpointRounding.AwayFromZero)
				: (int)Math.Round(normalized*8191,MidpointRounding.AwayFromZero);
			BinaryPrimitives.WriteInt16LittleEndian(
				data.AsSpan(index*2,2),
				(short)code);
		}
		return FromBytes(Path.GetFileName(path),data);
	}

	private static int ParseInteger(
		IReadOnlyDictionary<string,string> metadata,
		string key)
	{
		if(!metadata.TryGetValue(key,out string? text) ||
			!int.TryParse(text,NumberStyles.Integer,CultureInfo.InvariantCulture,out int value))
		{
			throw new InvalidDataException($"Brak prawidłowego pola {key} w EasyWave CSV.");
		}
		return value;
	}

	private static double ParseNumber(
		IReadOnlyDictionary<string,string> metadata,
		string key)
	{
		if(!metadata.TryGetValue(key,out string? text))
		{
			throw new InvalidDataException($"Brak pola {key} w EasyWave CSV.");
		}
		return ParseNumber(text,key);
	}

	private static double ParseNumber(string text,string field)
	{
		if(!double.TryParse(
			text.Trim(),
			NumberStyles.Float,
			CultureInfo.InvariantCulture,
			out double value) || !double.IsFinite(value))
		{
			throw new InvalidDataException($"Nieprawidłowa wartość pola {field} w EasyWave CSV.");
		}
		return value;
	}

	public static ArbitraryWaveformData FromBytes(
		string fileName,
		byte[] data)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
		ArgumentNullException.ThrowIfNull(data);
		if(data.Length<4 || data.Length>MaximumBytes || data.Length%2 != 0)
		{
			throw new InvalidDataException(
				"Plik musi zawierać od 2 do 16 384 próbek 16-bitowych.");
		}
		string source=Path.GetFileNameWithoutExtension(fileName);
		string name=new string(source.Select(character=>
			character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_'
				? character
				: '_').ToArray()).Trim('_');
		if(name.Length == 0)
		{
			name="waveform";
		}
		if(name.Length>16)
		{
			name=name[..16];
		}
		return new(name,[..data]);
	}
}
