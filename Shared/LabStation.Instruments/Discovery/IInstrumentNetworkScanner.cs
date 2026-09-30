using LabStation.Instruments.Scpi;

namespace LabStation.Instruments.Discovery;

public sealed record DiscoveredInstrument(
	string Address,
	ScpiIdentity Identity);

public interface IInstrumentNetworkScanner
{
	Task<DiscoveredInstrument?> FindFirstAsync(
		Func<ScpiIdentity,bool> isSupported,
		CancellationToken cancellationToken);
}

public interface IInstrumentIdentityProbe
{
	ValueTask<ScpiIdentity?> IdentifyAsync(
		string address,
		CancellationToken cancellationToken);
}
