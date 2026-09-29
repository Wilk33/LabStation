using System.Globalization;

namespace Scope.Core;

public sealed record ChannelMeasurements(
	int Channel,
	double? Vpp,
	double? Vrms,
	double? Frequency,
	double? Minimum,
	double? Maximum,
	double? DutyPercent)
{
	public static ChannelMeasurements Parse(int channel,string response)
	{
		int marker=response.IndexOf("PAVA ",StringComparison.OrdinalIgnoreCase);
		if(marker < 0)
		{
			throw new InvalidDataException("Nieprawidłowa odpowiedź PAVA: "+response);
		}
		string[] parts=response[(marker+5)..].Split(',',StringSplitOptions.TrimEntries);
		if(parts.Length%2 != 0)
		{
			throw new InvalidDataException("Niepełna odpowiedź PAVA: "+response);
		}
		Dictionary<string,string> values=new(StringComparer.OrdinalIgnoreCase);
		for(int i=0;i < parts.Length;i+=2)
		{
			values[parts[i]]=parts[i+1];
		}
		double? Number(string name)
		{
			if(!values.TryGetValue(name,out string? value))
			{
				return null;
			}
			try
			{
				return Scpi.Number(value);
			}
			catch(InvalidDataException)
			{
				return null;
			}
		}
		double? Percent(string name)
		{
			if(!values.TryGetValue(name,out string? value) ||
				!double.TryParse(value.TrimEnd('%'),NumberStyles.Float,CultureInfo.InvariantCulture,out double result) ||
				!double.IsFinite(result))
			{
				return null;
			}
			return result;
		}
		return new(
			channel,
			Number("PKPK"),
			Number("RMS"),
			Number("FREQ"),
			Number("MIN"),
			Number("MAX"),
			Percent("DUTY"));
	}
}
