using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using LabStation.Instruments.Scpi;

namespace LabStation.Instruments.Discovery;

public sealed class InstrumentNetworkScanner : IInstrumentNetworkScanner
{
	private readonly Func<IReadOnlyList<IPAddress>> addressProvider;
	private readonly IInstrumentIdentityProbe probe;
	private readonly int maximumConcurrency;

	public InstrumentNetworkScanner()
		: this(LocalAddresses,new Vxi11IdentityProbe(),16)
	{
	}

	public InstrumentNetworkScanner(
		Func<IReadOnlyList<IPAddress>> addressProvider,
		IInstrumentIdentityProbe probe,
		int maximumConcurrency=16)
	{
		ArgumentNullException.ThrowIfNull(addressProvider);
		ArgumentNullException.ThrowIfNull(probe);
		if(maximumConcurrency<1)
		{
			throw new ArgumentOutOfRangeException(nameof(maximumConcurrency));
		}
		this.addressProvider=addressProvider;
		this.probe=probe;
		this.maximumConcurrency=maximumConcurrency;
	}

	public async Task<DiscoveredInstrument?> FindFirstAsync(
		Func<ScpiIdentity,bool> isSupported,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(isSupported);
		IReadOnlyList<IPAddress> addresses=addressProvider();
		DiscoveredInstrument?[] matches=new DiscoveredInstrument?[addresses.Count];
		using SemaphoreSlim concurrency=new(maximumConcurrency);
		Task[] tasks=addresses.Select((address,index)=>ProbeAsync(
			address,
			index,
			matches,
			isSupported,
			concurrency,
			cancellationToken)).ToArray();
		await Task.WhenAll(tasks).ConfigureAwait(false);
		return matches.FirstOrDefault(match=>match is not null);
	}

	private async Task ProbeAsync(
		IPAddress address,
		int index,
		DiscoveredInstrument?[] matches,
		Func<ScpiIdentity,bool> isSupported,
		SemaphoreSlim concurrency,
		CancellationToken cancellationToken)
	{
		await concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			ScpiIdentity? identity=await probe.IdentifyAsync(
				address.ToString(),
				cancellationToken).ConfigureAwait(false);
			if(identity is not null && isSupported(identity))
			{
				matches[index]=new DiscoveredInstrument(address.ToString(),identity);
			}
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch(Exception)
		{
		}
		finally
		{
			concurrency.Release();
		}
	}

	private static IReadOnlyList<IPAddress> LocalAddresses()
	{
		return NetworkInterface.GetAllNetworkInterfaces()
			.Where(network=>
				network.OperationalStatus == OperationalStatus.Up &&
				network.NetworkInterfaceType != NetworkInterfaceType.Loopback)
			.SelectMany(network=>network.GetIPProperties().UnicastAddresses)
			.Where(value=>
				value.Address.AddressFamily == AddressFamily.InterNetwork &&
				value.IPv4Mask is not null &&
				!IPAddress.IsLoopback(value.Address) &&
				!value.Address.ToString().StartsWith("169.254.",StringComparison.Ordinal))
			.SelectMany(value=>Ipv4Subnet.Hosts(value.Address,value.IPv4Mask!))
			.Distinct()
			.OrderBy(address=>address.ToString(),StringComparer.Ordinal)
			.ToArray();
	}
}
