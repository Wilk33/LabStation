using System.Text;
using LabStation.Instruments.Scpi;
using LabStation.Instruments.Transport;

namespace Sdg1032X.Core;

public sealed class SiglentGeneratorClient(IInstrumentTransport transport) : IDisposable
{
	private readonly ScpiConnection connection=new(transport);

	public string Initialize()
	{
		string identity=connection.QueryText("*IDN?").Trim();
		ScpiIdentity parsed=ScpiIdentity.Parse(identity);
		if(!IsSupported(parsed))
		{
			throw new InvalidDataException(
				"Ta wersja aplikacji obsługuje SIGLENT SDG1032X. Odpowiedź: "+identity);
		}
		return identity;
	}

	public static bool IsSupported(ScpiIdentity identity)
	{
		return identity.Manufacturer.Contains(
			"SIGLENT",
			StringComparison.OrdinalIgnoreCase) &&
			identity.Model.Equals(
				"SDG1032X",
				StringComparison.OrdinalIgnoreCase);
	}

	public ChannelSnapshot ReadChannel(int channel)
	{
		string basic=connection.QueryText($"C{channel}:BSWV?").Trim();
		string output=connection.QueryText($"C{channel}:OUTP?").Trim();
		return SiglentProtocol.ParseSnapshot(channel,basic,output);
	}

	public void Write(string command)
	{
		connection.Write(command);
	}

	public void UploadArbitraryWaveform(
		int channel,
		ArbitraryWaveformData waveform,
		double frequencyHz,
		double amplitudeVpp,
		double offsetVolts,
		double phaseDegrees)
	{
		ArgumentNullException.ThrowIfNull(waveform);
		if(transport is not IRawInstrumentTransport raw)
		{
			throw new NotSupportedException(
				"Transport nie obsługuje binarnego zapisu przebiegu.");
		}
		byte[] header=Encoding.ASCII.GetBytes(
			SiglentProtocol.ArbitraryWaveformHeader(
				channel,
				waveform.Name,
				frequencyHz,
				amplitudeVpp,
				offsetVolts,
				phaseDegrees));
		byte[] payload=[..header,..waveform.Data];
		raw.Write(payload);
		connection.Write(
			SiglentProtocol.ArbitraryWaveformSelectCommand(
				channel,
				waveform.Name));
	}

	public void Dispose()
	{
		connection.Dispose();
	}
}
