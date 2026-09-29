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
		if(!parsed.Manufacturer.Contains("SIGLENT",StringComparison.OrdinalIgnoreCase) ||
			!parsed.Model.Equals("SDG1032X",StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException(
				"Ta wersja aplikacji obsługuje SIGLENT SDG1032X. Odpowiedź: "+identity);
		}
		return identity;
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

	public void Dispose()
	{
		connection.Dispose();
	}
}
