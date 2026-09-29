using System.Text;
using LabStation.Instruments.Transport;

namespace LabStation.Instruments.Scpi;

public sealed class ScpiConnection : IDisposable
{
	private readonly IInstrumentTransport transport;
	private readonly bool disposeTransport;

	public ScpiConnection(IInstrumentTransport transport,bool disposeTransport=true)
	{
		ArgumentNullException.ThrowIfNull(transport);
		this.transport=transport;
		this.disposeTransport=disposeTransport;
	}

	public void Write(string command)=>transport.Write(command);
	public byte[] QueryBytes(string command)=>transport.Query(command);
	public string QueryText(string command)=>
		Encoding.ASCII.GetString(QueryBytes(command)).TrimEnd('\r','\n');

	public ScpiIdentity Identify()=>ScpiIdentity.Parse(QueryText("*IDN?"));

	public void Dispose()
	{
		if(disposeTransport)
		{
			transport.Dispose();
		}
	}
}
