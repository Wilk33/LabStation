using Ka3005P.Core.Configuration;

namespace Ka3005P.Tests.Configuration;

public sealed class PortLeaseRegistryTests
{
	[Fact]
	public void TryAcquire_NormalizesNameAndPreventsDuplicateLease()
	{
		PortLeaseRegistry registry=new();

		Assert.True(registry.TryAcquire(" com5 ",out PortLease? first));
		Assert.False(registry.TryAcquire("COM5",out PortLease? duplicate));
		Assert.NotNull(first);
		Assert.Null(duplicate);

		first.Dispose();
		Assert.True(registry.TryAcquire("com5",out PortLease? afterRelease));
		afterRelease.Dispose();
	}
}
