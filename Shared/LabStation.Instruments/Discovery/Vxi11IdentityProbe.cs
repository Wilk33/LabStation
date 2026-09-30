using LabStation.Instruments.Scpi;
using LabStation.Instruments.Transport;

namespace LabStation.Instruments.Discovery;

public sealed class Vxi11IdentityProbe : IInstrumentIdentityProbe
{
	private static readonly Vxi11Options ScanOptions=new()
	{
		ConnectTimeout=TimeSpan.FromMilliseconds(500),
		IoTimeout=TimeSpan.FromSeconds(1)
	};

	public ValueTask<ScpiIdentity?> IdentifyAsync(
		string address,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(address);
		return new ValueTask<ScpiIdentity?>(Task.Run<ScpiIdentity?>(()=>
		{
			cancellationToken.ThrowIfCancellationRequested();
			using Vxi11Transport transport=new(address,ScanOptions);
			using ScpiConnection connection=new(transport,false);
			return connection.Identify();
		},cancellationToken));
	}
}
