using System.Globalization;

namespace Sdm3000.Core;

public static class MeasurementFormatter
{
	private static readonly CultureInfo Polish=CultureInfo.GetCultureInfo("pl-PL");

	public static string FormatValue(MeasurementReading reading,string unit)
	{
		if(reading.State == ReadingState.Overload || reading.Value is not double value)
		{
			return "OL";
		}
		(double scaled,string prefix)=Scale(value);
		return scaled.ToString("0.00000",Polish)+
			(prefix.Length == 0 && unit.Length == 0 ? "" : " ")+
			prefix+unit;
	}

	public static string FormatStatistic(double? value,string unit)
	{
		return value is double number
			? FormatValue(new(ReadingState.Value,number),unit)
			: "-";
	}

	public static string FormatRange(double? value,string unit)
	{
		if(value is not double number)
		{
			return "-";
		}
		(double scaled,string prefix)=Scale(number);
		return scaled.ToString("0.#####",Polish)+" "+prefix+unit;
	}

	private static (double Value,string Prefix) Scale(double value)
	{
		double magnitude=Math.Abs(value);
		if(magnitude >= 1e9)
		{
			return (value/1e9,"G");
		}
		if(magnitude >= 1e6)
		{
			return (value/1e6,"M");
		}
		if(magnitude >= 1e3)
		{
			return (value/1e3,"k");
		}
		if(magnitude > 0 && magnitude < 1e-6)
		{
			return (value/1e-9,"n");
		}
		if(magnitude > 0 && magnitude < 1e-3)
		{
			return (value/1e-6,"µ");
		}
		if(magnitude > 0 && magnitude < 1)
		{
			return (value/1e-3,"m");
		}
		return (value,"");
	}
}
