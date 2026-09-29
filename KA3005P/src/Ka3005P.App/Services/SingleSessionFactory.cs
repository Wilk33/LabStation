using Ka3005P.Core.Device;
using Ka3005P.Core.Sessions;
using Ka3005P.Core.Transport;

namespace Ka3005P.App.Services;

public interface ISingleSessionFactory
{
	ValueTask<IPowerSupplySession> CreateAsync(
		string portName,
		CancellationToken cancellationToken);
}

public sealed class SerialSingleSessionFactory : ISingleSessionFactory
{
	private readonly TimeProvider timeProvider;

	public SerialSingleSessionFactory(TimeProvider timeProvider)
	{
		ArgumentNullException.ThrowIfNull(timeProvider);
		this.timeProvider=timeProvider;
	}

	public async ValueTask<IPowerSupplySession> CreateAsync(
		string portName,
		CancellationToken cancellationToken)
	{
		SerialPortTransport transport=new(
			TimeSpan.FromMilliseconds(250),
			TimeSpan.FromMilliseconds(250));
		try
		{
			await transport.OpenAsync(portName,cancellationToken).ConfigureAwait(false);
			Ka3005PDevice device=new(transport,TimeSpan.FromMilliseconds(350));
			PowerSupplySession session=new(
				device,
				timeProvider,
				TimeSpan.FromMilliseconds(100));
			await session.StartAsync(cancellationToken).ConfigureAwait(false);
			return session;
		}
		catch
		{
			await transport.DisposeAsync().ConfigureAwait(false);
			throw;
		}
	}
}
