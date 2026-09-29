using System.Globalization;

namespace Ka3005P.Core.Measurements;

public static class ResistanceFormatter
{
	public static bool TryFormat(
		int voltageHundredths,
		int currentThousandths,
		out string? value)
	{
		if(voltageHundredths < 0 || currentThousandths <= 0)
		{
			value=null;
			return false;
		}

		double ohms=10d*voltageHundredths/currentThousandths;
		(double scaled,string unit)=ohms switch
		{
			< 0.001=>(ohms*1_000_000d,"µΩ"),
			< 1=>(ohms*1_000d,"mΩ"),
			< 1_000=>(ohms,"Ω"),
			_=>(ohms/1_000d,"kΩ")
		};
		value=scaled.ToString("0.###",CultureInfo.InvariantCulture)+" "+unit;
		return true;
	}
}
