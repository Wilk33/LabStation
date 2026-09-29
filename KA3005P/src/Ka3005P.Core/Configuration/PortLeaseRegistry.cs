using System.Diagnostics.CodeAnalysis;

namespace Ka3005P.Core.Configuration;

public sealed class PortLeaseRegistry
{
	private readonly object gate=new();
	private readonly HashSet<string> leasedPorts=
		new(StringComparer.OrdinalIgnoreCase);

	public bool TryAcquire(
		string portName,
		[NotNullWhen(true)] out PortLease? lease)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(portName);
		string key=portName.Trim().ToUpperInvariant();
		lock(gate)
		{
			if(!leasedPorts.Add(key))
			{
				lease=null;
				return false;
			}

			lease=new PortLease(key,Release);
			return true;
		}
	}

	private void Release(string portName)
	{
		lock(gate)
		{
			leasedPorts.Remove(portName);
		}
	}
}

public sealed class PortLease : IDisposable
{
	private readonly Action<string> release;
	private int released;

	internal PortLease(string portName,Action<string> release)
	{
		PortName=portName;
		this.release=release;
	}

	public string PortName { get; }

	public void Dispose()
	{
		if(Interlocked.Exchange(ref released,1) == 0)
		{
			release(PortName);
		}
	}
}
