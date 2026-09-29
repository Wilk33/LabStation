using Ka3005P.App.Services;

namespace Ka3005P.Tests.Services;

public sealed class SerialPortMonitorTests
{
	[Fact]
	public async Task RefreshOnce_PublishesOnlyChangedSnapshots()
	{
		MutableCatalog catalog=new(["COM2"]);
		await using SerialPortMonitor monitor=new(catalog,TimeSpan.FromSeconds(1));
		List<string[]> published=[];
		monitor.PortsChanged+=(_,ports)=>published.Add([.. ports]);

		await monitor.RefreshOnceAsync(CancellationToken.None);
		await monitor.RefreshOnceAsync(CancellationToken.None);
		catalog.Ports=["COM2","COM10"];
		await monitor.RefreshOnceAsync(CancellationToken.None);

		Assert.Equal(2,published.Count);
		Assert.Equal(["COM2"],published[0]);
		Assert.Equal(["COM2","COM10"],published[1]);
	}

	[Fact]
	public async Task RefreshOnce_FailureKeepsLastSnapshot()
	{
		MutableCatalog catalog=new(["COM5"]);
		await using SerialPortMonitor monitor=new(catalog,TimeSpan.FromSeconds(1));
		await monitor.RefreshOnceAsync(CancellationToken.None);
		catalog.Error=new IOException("katalog");

		await monitor.RefreshOnceAsync(CancellationToken.None);

		Assert.Equal(["COM5"],monitor.Ports);
		Assert.IsType<IOException>(monitor.LastError);
	}

	private sealed class MutableCatalog : ISerialPortCatalog
	{
		public MutableCatalog(IReadOnlyList<string> ports)
		{
			Ports=ports;
		}

		public IReadOnlyList<string> Ports { get; set; }
		public Exception? Error { get; set; }

		public IReadOnlyList<string> GetPortNames()
		{
			if(Error is not null)
			{
				throw Error;
			}
			return Ports;
		}
	}
}
