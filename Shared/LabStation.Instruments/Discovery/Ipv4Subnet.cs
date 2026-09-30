using System.Net;

namespace LabStation.Instruments.Discovery;

public static class Ipv4Subnet
{
	public static IReadOnlyList<IPAddress> Hosts(
		IPAddress localAddress,
		IPAddress subnetMask)
	{
		ArgumentNullException.ThrowIfNull(localAddress);
		ArgumentNullException.ThrowIfNull(subnetMask);
		byte[] address=localAddress.GetAddressBytes();
		byte[] mask=subnetMask.GetAddressBytes();
		if(address.Length != 4 || mask.Length != 4)
		{
			throw new ArgumentException("Wymagany jest adres i maska IPv4.");
		}
		int prefix=PrefixLength(mask);
		if(prefix<24)
		{
			mask=[255,255,255,0];
		}
		uint local=ToUInt32(address);
		uint maskValue=ToUInt32(mask);
		uint network=local&maskValue;
		uint broadcast=network|~maskValue;
		List<IPAddress> result=[];
		for(uint value=network+1;value<broadcast;value++)
		{
			if(value != local)
			{
				result.Add(FromUInt32(value));
			}
		}
		return result;
	}

	private static int PrefixLength(byte[] mask)
	{
		int prefix=0;
		bool zeroFound=false;
		foreach(byte value in mask)
		{
			for(int bit=7;bit>=0;bit--)
			{
				bool set=(value&(1<<bit)) != 0;
				if(set && zeroFound)
				{
					throw new ArgumentException("Maska IPv4 nie jest ciągła.");
				}
				if(set)
				{
					prefix++;
				}
				else
				{
					zeroFound=true;
				}
			}
		}
		return prefix;
	}

	private static uint ToUInt32(byte[] bytes)=>
		((uint)bytes[0]<<24)|
		((uint)bytes[1]<<16)|
		((uint)bytes[2]<<8)|
		bytes[3];

	private static IPAddress FromUInt32(uint value)=>new(
	[
		(byte)(value>>24),
		(byte)(value>>16),
		(byte)(value>>8),
		(byte)value
	]);
}
