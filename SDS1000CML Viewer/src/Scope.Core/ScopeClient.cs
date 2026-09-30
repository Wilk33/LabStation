using System.Text;
using LabStation.Instruments.Scpi;
using LabStation.Instruments.Transport;

namespace Scope.Core;


public enum AcquisitionState

{

	Unknown,
	Start,
	Stop

}


public sealed class ScopeClient : IDisposable

{
	private readonly ScpiConnection connection;

	public ScopeClient(IInstrumentTransport transport)
	{
		connection=new(transport);
	}

	public string Identity
	{
		get; private set;
	}
="";
	public string TriggerMode
	{
		get; private set;
	}
="";
	public string Initialize()

	{

		Identity=Text("*IDN?");
		ScpiIdentity parsed=ScpiIdentity.Parse(Identity);
		if(!IsSupported(parsed))
			throw new InvalidDataException("Ta wersja aplikacji obsługuje SDS1102CML+. Odpowiedź: "+Identity);
		return Identity;

	}

	public static bool IsSupported(ScpiIdentity identity)
	{
		return identity.Manufacturer.Contains(
			"SIGLENT",
			StringComparison.OrdinalIgnoreCase) &&
			identity.Model.Equals(
				"SDS1102CML+",
				StringComparison.OrdinalIgnoreCase);
	}

	private string Text(string command)=>connection.QueryText(command).Trim();
	public Waveform[] Capture(int[] channels)

	{

		if (channels.Length == 0 || channels.Any(c => c != 1 && c != 2))
			throw new ArgumentException("Wybierz CH1 lub CH2.");
		// Only transfer parameters are changed. Never alter acquisition or channel settings.
		connection.Write("WFSU SP,1,NP,0,FP,0");
		List<Waveform> result=[];
		foreach (int channel in channels.Distinct())

		{

			string enabled=Text($"C{channel}:TRA?").ToUpperInvariant();
			if (enabled.EndsWith("OFF"))
				continue;
			if (!enabled.EndsWith("ON"))
				throw new InvalidDataException("Nieznany stan kanału: "+enabled);
			result.Add(Waveform.Decode(channel,connection.QueryBytes($"C{channel}:WF? ALL")));

		}

		if (result.Count == 0)
			throw new InvalidOperationException("Wybrane kanały są wyłączone na oscyloskopie.");
		return result.ToArray();

	}

	public ChannelMeasurements[] Measurements(int[] channels)

	{

		if(channels.Length == 0 || channels.Any(channel=>channel != 1 && channel != 2))
			throw new ArgumentException("Wybierz CH1 lub CH2.");
		return channels.Distinct()
			.Select(channel=>ChannelMeasurements.Parse(
				channel,
				Text($"C{channel}:PAVA? PKPK,RMS,FREQ,MIN,MAX,DUTY")))
			.ToArray();

	}
	public AcquisitionState AcquisitionStatus()

	{

		string[] parts=Text("SAST?").Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length < 2 || !parts[0].Equals("SAST", StringComparison.OrdinalIgnoreCase))
			return AcquisitionState.Unknown;
		string value=parts[^1].ToUpperInvariant();
		if (value == "STOP")
			return AcquisitionState.Stop;
		return AcquisitionState.Start;

	}
	public void Stop()

	{

		string mode=Text("TRMD?").Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1].ToUpperInvariant();
		if (mode is "AUTO" or "NORM" or "NORMAL" or "SINGLE")
			TriggerMode=mode == "NORMAL" ? "NORM" : mode;
		connection.Write("STOP");

	}

	public void Start()

	{

		// Restore the observed trigger mode; AUTO is the documented continuous fallback.
		string mode=Text("TRMD?").Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1].ToUpperInvariant();
		if (mode is "AUTO" or "NORM" or "NORMAL" or "SINGLE")
			TriggerMode=mode == "NORMAL" ? "NORM" : mode;
		connection.Write("TRMD "+(TriggerMode.Length > 0 ? TriggerMode : "AUTO"));

	}

	public void Auto()=>connection.Write("ASET");
	public void Dispose()=>connection.Dispose();

}
