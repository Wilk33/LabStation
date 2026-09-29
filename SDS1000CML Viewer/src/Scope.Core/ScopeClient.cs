using System.Text;

namespace Scope.Core;

public interface IInstrumentTransport : IDisposable

{

	void Write(string command);
	byte[] Query(string command);

}


public enum AcquisitionState

{

	Unknown,
	Start,
	Stop

}


public sealed class ScopeClient(IInstrumentTransport transport) : IDisposable

{

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
		string[] parts=Identity.Split(',');
		if (parts.Length < 2 || !parts[0].Contains("SIGLENT", StringComparison.OrdinalIgnoreCase) || !parts[1].Trim().Equals("SDS1102CML+", StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException("Ta wersja aplikacji obsługuje SDS1102CML+. Odpowiedź: "+Identity);
		return Identity;

	}

	private string Text(string command) => Encoding.ASCII.GetString(transport.Query(command)).Trim();
	public Waveform[] Capture(int[] channels)

	{

		if (channels.Length == 0 || channels.Any(c => c != 1 && c != 2))
			throw new ArgumentException("Wybierz CH1 lub CH2.");
		// Only transfer parameters are changed. Never alter acquisition or channel settings.
		transport.Write("WFSU SP,1,NP,0,FP,0");
		List<Waveform> result=[];
		foreach (int channel in channels.Distinct())

		{

			string enabled=Text($"C{channel}:TRA?").ToUpperInvariant();
			if (enabled.EndsWith("OFF"))
				continue;
			if (!enabled.EndsWith("ON"))
				throw new InvalidDataException("Nieznany stan kanału: "+enabled);
			result.Add(Waveform.Decode(channel, transport.Query($"C{channel}:WF? ALL")));

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
		transport.Write("STOP");

	}

	public void Start()

	{

		// Restore the observed trigger mode; AUTO is the documented continuous fallback.
		string mode=Text("TRMD?").Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1].ToUpperInvariant();
		if (mode is "AUTO" or "NORM" or "NORMAL" or "SINGLE")
			TriggerMode=mode == "NORMAL" ? "NORM" : mode;
		transport.Write("TRMD "+(TriggerMode.Length > 0 ? TriggerMode : "AUTO"));

	}

	public void Auto() => transport.Write("ASET");
	public void Dispose() => transport.Dispose();

}
