using System.Globalization;
using System.Text.RegularExpressions;

namespace Sdm3000.Core;

public static partial class MultimeterProtocol
{
	private const double OverloadThreshold=9e36;

	public static MeasurementConfiguration ParseConfiguration(string response)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(response);
		string normalized=response.Trim().Trim('"').ToUpperInvariant();
		MeasurementFunction function=ParseFunction(normalized);
		Match rangeMatch=NumberPattern().Match(normalized);
		double? range=rangeMatch.Success
			? double.Parse(rangeMatch.Value,CultureInfo.InvariantCulture)
			: null;
		return new(function,range);
	}

	public static MeasurementReading ParseReading(string response)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(response);
		Match match=NumberPattern().Match(response.Trim());
		if(!match.Success || !double.TryParse(
			match.Value,
			NumberStyles.Float,
			CultureInfo.InvariantCulture,
			out double value))
		{
			throw new InvalidDataException("Nieprawidłowy wynik pomiaru: "+response);
		}
		if(!double.IsFinite(value) || Math.Abs(value) >= OverloadThreshold)
		{
			return new(ReadingState.Overload,null);
		}
		return new(ReadingState.Value,value);
	}

	public static long ParsePointCount(string response)
	{
		if(!long.TryParse(response.Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out long value) || value < 0)
		{
			throw new InvalidDataException("Nieprawidłowa liczba punktów: "+response);
		}
		return value;
	}

	private static MeasurementFunction ParseFunction(string value)
	{
		if(value.Contains("VOLT:AC",StringComparison.Ordinal))
		{
			return MeasurementFunction.VoltageAc;
		}
		if(value.Contains("VOLT",StringComparison.Ordinal))
		{
			return MeasurementFunction.VoltageDc;
		}
		if(value.Contains("CURR:AC",StringComparison.Ordinal))
		{
			return MeasurementFunction.CurrentAc;
		}
		if(value.Contains("CURR",StringComparison.Ordinal))
		{
			return MeasurementFunction.CurrentDc;
		}
		if(value.Contains("FRES",StringComparison.Ordinal))
		{
			return MeasurementFunction.Resistance4Wire;
		}
		if(value.Contains("RES",StringComparison.Ordinal))
		{
			return MeasurementFunction.Resistance2Wire;
		}
		if(value.Contains("CAP",StringComparison.Ordinal))
		{
			return MeasurementFunction.Capacitance;
		}
		if(value.Contains("CONT",StringComparison.Ordinal))
		{
			return MeasurementFunction.Continuity;
		}
		if(value.Contains("DIOD",StringComparison.Ordinal))
		{
			return MeasurementFunction.Diode;
		}
		if(value.Contains("FREQ",StringComparison.Ordinal))
		{
			return MeasurementFunction.Frequency;
		}
		if(value.Contains("PER",StringComparison.Ordinal))
		{
			return MeasurementFunction.Period;
		}
		if(value.Contains("TEMP",StringComparison.Ordinal))
		{
			return MeasurementFunction.Temperature;
		}
		return MeasurementFunction.Unknown;
	}

	[GeneratedRegex(@"[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[Ee][+-]?\d+)?",RegexOptions.CultureInvariant)]
	private static partial Regex NumberPattern();
}
