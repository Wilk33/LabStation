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
		return FromBytes(Path.GetFileName(path),File.ReadAllBytes(path));
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
