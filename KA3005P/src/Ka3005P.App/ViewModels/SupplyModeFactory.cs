using Ka3005P.App.Services;
using Ka3005P.Core.Configuration;

namespace Ka3005P.App.ViewModels;

public sealed class SupplyModeFactory : ISupplyModeFactory
{
	private readonly PortLeaseRegistry leases;
	private readonly ISingleSessionFactory sessionFactory;

	public SupplyModeFactory(
		PortLeaseRegistry leases,
		ISingleSessionFactory sessionFactory)
	{
		this.leases=leases;
		this.sessionFactory=sessionFactory;
	}

	public ISupplyModeViewModel Create(
		ApplicationMode mode,
		IReadOnlyList<string> ports,
		AppSettings settings)
	{
		return mode switch
		{
			ApplicationMode.Single=>new SingleSupplyViewModel(
				leases,
				sessionFactory,
				ports,
				settings.SinglePort),
			ApplicationMode.Dual=>new DualSupplyViewModel(
				leases,
				sessionFactory,
				ports,
				settings.DualFirstPort,
				settings.DualSecondPort),
			_=>throw new ArgumentOutOfRangeException(nameof(mode))
		};
	}
}
