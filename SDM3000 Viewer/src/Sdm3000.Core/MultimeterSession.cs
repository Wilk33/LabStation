using LabStation.Instruments.Scpi;
using LabStation.Instruments.Transport;

namespace Sdm3000.Core;

public sealed class MultimeterSession : IAsyncDisposable
{
	private readonly SiglentMultimeterClient client;
	private readonly MeasurementAccumulator accumulator=new();
	private readonly SemaphoreSlim ioGate=new(1,1);
	private readonly CancellationTokenSource cancellation=new();
	private bool disposed;

	private MultimeterSession(
		SiglentMultimeterClient client,
		ScpiIdentity identity)
	{
		this.client=client;
		Identity=identity;
	}

	public ScpiIdentity Identity { get; }

	public static async Task<MultimeterSession> ConnectAsync(string host)
	{
		return await CreateAsync(()=>new Vxi11Transport(host));
	}

	public static async Task<MultimeterSession> CreateAsync(
		Func<IInstrumentTransport> transportFactory)
	{
		ArgumentNullException.ThrowIfNull(transportFactory);
		return await Task.Run(() =>
		{
			IInstrumentTransport transport=transportFactory();
			try
			{
				SiglentMultimeterClient client=new(transport);
				ScpiIdentity identity=client.Initialize();
				return new MultimeterSession(client,identity);
			}
			catch
			{
				transport.Dispose();
				throw;
			}
		});
	}

	public async Task<MeasurementSnapshot> ReadAsync()
	{
		ObjectDisposedException.ThrowIf(disposed,this);
		await ioGate.WaitAsync(cancellation.Token);
		try
		{
			return await Task.Run(
				()=>client.ReadSnapshot(accumulator),
				cancellation.Token);
		}
		finally
		{
			ioGate.Release();
		}
	}

	public async ValueTask DisposeAsync()
	{
		if(disposed)
		{
			return;
		}
		disposed=true;
		cancellation.Cancel();
		await ioGate.WaitAsync();
		try
		{
			client.Dispose();
		}
		finally
		{
			ioGate.Release();
			ioGate.Dispose();
			cancellation.Dispose();
		}
	}
}
