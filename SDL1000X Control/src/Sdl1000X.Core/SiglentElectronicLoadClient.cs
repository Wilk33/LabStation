using LabStation.Instruments.Scpi;
using LabStation.Instruments.Transport;

namespace Sdl1000X.Core;

public sealed class SiglentElectronicLoadClient : IDisposable
{
	private readonly ScpiConnection connection;

	public SiglentElectronicLoadClient(IInstrumentTransport transport)
	{
		connection=new(transport);
	}

	public ScpiIdentity Initialize()
	{
		ScpiIdentity identity=connection.Identify();
		if(!IsSupported(identity))
		{
			throw new InvalidDataException(
				"Ta wersja aplikacji obsługuje obciążenie SIGLENT SDL1020X-E. Odpowiedź: "+
				string.Join(',',identity.Manufacturer,identity.Model,identity.SerialNumber,identity.Firmware));
		}
		return identity;
	}

	public static bool IsSupported(ScpiIdentity identity)
	{
		return identity.Manufacturer.Contains(
			"SIGLENT",
			StringComparison.OrdinalIgnoreCase) &&
			identity.Model.StartsWith(
				"SDL1020X",
				StringComparison.OrdinalIgnoreCase);
	}

	public ElectronicLoadSnapshot ReadSnapshot()
	{
		bool inputEnabled=QueryInputEnabled();
		LoadMode mode=ElectronicLoadProtocol.ParseMode(
			connection.QueryText(":SOURce:FUNCtion?"));
		double? setpoint=null;
		LedSettings? led=null;
		if(mode == LoadMode.Led)
		{
			led=ReadLedSettings();
		}
		else
		{
			string query=ElectronicLoadProtocol.SetpointQuery(mode);
			setpoint=ElectronicLoadProtocol.ParseNumber(
				connection.QueryText(query),
				query);
		}
		ProtectionSettings protections=ReadProtections();
		LoadMeasurements measurements=new(
			QueryNumber("MEASure:VOLTage:DC?"),
			QueryNumber("MEASure:CURRent:DC?"),
			QueryNumber("MEASure:POWer:DC?"),
			QueryNumber("MEASure:RESistance:DC?"));
		return new(
			mode,
			inputEnabled,
			setpoint,
			led,
			protections,
			measurements,
			DateTimeOffset.UtcNow);
	}

	public void SetMode(LoadMode mode)
	{
		EnsureInputOff();
		connection.Write(ElectronicLoadProtocol.ModeCommand(mode));
	}

	public void SetSetpoint(LoadMode mode,double value)
	{
		string command=ElectronicLoadProtocol.SetpointCommand(mode,value);
		EnsureInputOff();
		connection.Write(command);
	}

	public void SetLedSettings(LedSettings settings)
	{
		ElectronicLoadProtocol.ValidateLed(settings);
		EnsureInputOff();
		connection.Write(":SOURce:LED:VOLTage "+ElectronicLoadProtocol.Number(settings.Voltage));
		connection.Write(":SOURce:LED:CURRent "+ElectronicLoadProtocol.Number(settings.Current));
		connection.Write(":SOURce:LED:RCOnf "+ElectronicLoadProtocol.Number(settings.Resistance));
	}

	public void SetProtections(ProtectionSettings settings)
	{
		string current=ElectronicLoadProtocol.OverCurrentLevelCommand(settings.OverCurrentAmps);
		string power=ElectronicLoadProtocol.OverPowerLevelCommand(settings.OverPowerWatts);
		EnsureInputOff();
		connection.Write(current);
		connection.Write(
			":SOURce:CURRent:PROTection:STATe "+
			(settings.OverCurrentEnabled ? "ON" : "OFF"));
		connection.Write(power);
		connection.Write(
			":SOURce:POWer:PROTection:STATe "+
			(settings.OverPowerEnabled ? "ON" : "OFF"));
	}

	public void SetInput(bool enabled)
	{
		bool current=QueryInputEnabled();
		if(current == enabled)
		{
			return;
		}
		if(enabled)
		{
			ValidateActiveSettings();
		}
		connection.Write(":SOURce:INPut:STATe "+(enabled ? "ON" : "OFF"));
		if(QueryInputEnabled() != enabled)
		{
			throw new IOException("Urządzenie nie potwierdziło stanu wejścia "+(enabled ? "ON" : "OFF")+".");
		}
	}

	public bool QueryInputEnabled()
	{
		const string command=":SOURce:INPut:STATe?";
		return ElectronicLoadProtocol.ParseBoolean(
			connection.QueryText(command),
			command);
	}

	public void Dispose()
	{
		connection.Dispose();
	}

	private void EnsureInputOff()
	{
		if(QueryInputEnabled())
		{
			throw new InvalidOperationException("Najpierw wyłącz wejście obciążenia.");
		}
	}

	private void ValidateActiveSettings()
	{
		LoadMode mode=ElectronicLoadProtocol.ParseMode(
			connection.QueryText(":SOURce:FUNCtion?"));
		if(mode == LoadMode.Led)
		{
			ElectronicLoadProtocol.ValidateLed(ReadLedSettings());
			return;
		}
		string query=ElectronicLoadProtocol.SetpointQuery(mode);
		double value=ElectronicLoadProtocol.ParseNumber(
			connection.QueryText(query),
			query);
		ElectronicLoadProtocol.ValidateSetpoint(mode,value);
	}

	private LedSettings ReadLedSettings()
	{
		return new(
			QueryNumber(":SOURce:LED:VOLTage?"),
			QueryNumber(":SOURce:LED:CURRent?"),
			QueryNumber(":SOURce:LED:RCOnf?"));
	}

	private ProtectionSettings ReadProtections()
	{
		const string currentState=":SOURce:CURRent:PROTection:STATe?";
		const string powerState=":SOURce:POWer:PROTection:STATe?";
		return new(
			ElectronicLoadProtocol.ParseBoolean(connection.QueryText(currentState),currentState),
			QueryNumber(":SOURce:CURRent:PROTection:LEVel?"),
			ElectronicLoadProtocol.ParseBoolean(connection.QueryText(powerState),powerState),
			QueryNumber(":SOURce:POWer:PROTection:LEVel?"));
	}

	private double QueryNumber(string command)
	{
		return ElectronicLoadProtocol.ParseNumber(
			connection.QueryText(command),
			command);
	}
}
