using System.Globalization;

namespace Sdl1000X.Core;

public static class ElectronicLoadProtocol
{
	public const double MaximumInputVoltage=150;
	public const double MaximumInputCurrent=30;
	public const double MaximumInputPower=200;
	public const double MaximumOverCurrentProtection=31;
	public const double MaximumOverPowerProtection=210;
	public const double OverVoltageProtection=155;
	public const double OverTemperatureProtection=85;

	public static LoadMode ParseMode(string response)
	{
		return response.Trim().ToUpperInvariant() switch
		{
			"CURRENT" or "CURR"=>LoadMode.ConstantCurrent,
			"VOLTAGE" or "VOLT"=>LoadMode.ConstantVoltage,
			"POWER" or "POW"=>LoadMode.ConstantPower,
			"RESISTANCE" or "RES"=>LoadMode.ConstantResistance,
			"LED"=>LoadMode.Led,
			_=>throw new InvalidDataException("Nieznany tryb obciążenia: "+response.Trim())
		};
	}

	public static bool ParseBoolean(string response,string command)
	{
		return response.Trim().ToUpperInvariant() switch
		{
			"1" or "ON"=>true,
			"0" or "OFF"=>false,
			_=>throw new InvalidDataException("Nieprawidłowa odpowiedź "+command+": "+response.Trim())
		};
	}

	public static double ParseNumber(string response,string command)
	{
		if(response.Contains(',') ||
			!double.TryParse(
				response.Trim(),
				NumberStyles.Float,
				CultureInfo.InvariantCulture,
				out double value) ||
			!double.IsFinite(value))
		{
			throw new InvalidDataException("Nieprawidłowa odpowiedź "+command+": "+response.Trim());
		}
		return value;
	}

	public static string ModeCommand(LoadMode mode)
	{
		return ":SOURce:FUNCtion "+ModeToken(mode);
	}

	public static string SetpointQuery(LoadMode mode)
	{
		return mode switch
		{
			LoadMode.ConstantCurrent=>":SOURce:CURRent:LEVel:IMMediate?",
			LoadMode.ConstantVoltage=>":SOURce:VOLTage:LEVel:IMMediate?",
			LoadMode.ConstantPower=>":SOURce:POWer:LEVel:IMMediate?",
			LoadMode.ConstantResistance=>":SOURce:RESistance:LEVel:IMMediate?",
			_=>throw new ArgumentOutOfRangeException(nameof(mode),"Tryb LED ma trzy oddzielne nastawy.")
		};
	}

	public static string SetpointCommand(LoadMode mode,double value)
	{
		ValidateSetpoint(mode,value);
		return SetpointQuery(mode).TrimEnd('?')+" "+Number(value);
	}

	public static string OverCurrentLevelCommand(double value)
	{
		ValidateRange(value,0,MaximumOverCurrentProtection,nameof(value));
		return ":SOURce:CURRent:PROTection:LEVel "+Number(value);
	}

	public static string OverPowerLevelCommand(double value)
	{
		ValidateRange(value,0,MaximumOverPowerProtection,nameof(value));
		return ":SOURce:POWer:PROTection:LEVel "+Number(value);
	}

	public static void ValidateSetpoint(LoadMode mode,double value)
	{
		if(mode == LoadMode.Led)
		{
			throw new ArgumentOutOfRangeException(nameof(mode),"Tryb LED ma trzy oddzielne nastawy.");
		}
		LoadModeProfile profile=LoadModeProfiles.For(mode);
		ValidateRange(value,profile.Minimum,profile.Maximum,nameof(value));
	}

	public static void ValidateLed(LedSettings settings)
	{
		ArgumentNullException.ThrowIfNull(settings);
		ValidateRange(settings.Voltage,0,MaximumInputVoltage,nameof(settings.Voltage));
		ValidateRange(settings.Current,0,MaximumInputCurrent,nameof(settings.Current));
		ValidateRange(settings.Resistance,0.03,10000,nameof(settings.Resistance));
	}

	public static string Number(double value)
	{
		if(!double.IsFinite(value))
		{
			throw new ArgumentOutOfRangeException(nameof(value));
		}
		return value.ToString("G17",CultureInfo.InvariantCulture);
	}

	public static string ModeToken(LoadMode mode)
	{
		return mode switch
		{
			LoadMode.ConstantCurrent=>"CURRent",
			LoadMode.ConstantVoltage=>"VOLTage",
			LoadMode.ConstantPower=>"POWer",
			LoadMode.ConstantResistance=>"RESistance",
			LoadMode.Led=>"LED",
			_=>throw new ArgumentOutOfRangeException(nameof(mode))
		};
	}

	private static void ValidateRange(double value,double minimum,double maximum,string parameter)
	{
		if(!double.IsFinite(value) || value<minimum || value>maximum)
		{
			throw new ArgumentOutOfRangeException(
				parameter,
				value,
				$"Dozwolony zakres: {minimum:G17} do {maximum:G17}.");
		}
	}
}
