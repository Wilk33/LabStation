using LabStation.Instruments.Scheduling;
using LabStation.Instruments.Transport;

namespace Sdg1032X.Core;

public sealed class GeneratorSession : IAsyncDisposable
{
	private readonly SiglentGeneratorClient client;
	private readonly LatestRequestQueue<GeneratorWrite> requests=new();
	private readonly SemaphoreSlim ioGate=new(1,1);
	private readonly CancellationTokenSource cancellation=new();
	private readonly Task worker;
	private bool disposed;

	private GeneratorSession(SiglentGeneratorClient generatorClient,string identity)
	{
		client=generatorClient;
		Identity=identity;
		worker=Task.Run(ProcessWritesAsync);
	}

	public event EventHandler<Exception>? CommunicationFailed;
	public string Identity { get; }

	public static async Task<GeneratorSession> ConnectAsync(string host)
	{
		return await CreateAsync(()=>new Vxi11Transport(host));
	}

	public static async Task<GeneratorSession> CreateAsync(Func<IInstrumentTransport> transportFactory)
	{
		return await Task.Run(()=>
		{
			IInstrumentTransport transport=transportFactory();
			try
			{
				SiglentGeneratorClient client=new(transport);
				string identity=client.Initialize();
				return new GeneratorSession(client,identity);
			}
			catch
			{
				transport.Dispose();
				throw;
			}
		});
	}

	public Task SetLatestAsync(string key,string command)
	{
		ObjectDisposedException.ThrowIf(disposed,this);
		return requests.EnqueueLatest(
			key,
			new(generator=>generator.Write(command))).Completion;
	}

	public Task WritePriorityAsync(string command)
	{
		ObjectDisposedException.ThrowIf(disposed,this);
		return requests.EnqueuePriority(
			new(generator=>generator.Write(command))).Completion;
	}

	public Task WriteOrderedAsync(string command)
	{
		ObjectDisposedException.ThrowIf(disposed,this);
		return requests.EnqueueOrdered(
			new(generator=>generator.Write(command))).Completion;
	}

	public Task UploadArbitraryWaveformAsync(
		int channel,
		ArbitraryWaveformData waveform,
		double frequencyHz,
		double amplitudeVpp,
		double offsetVolts,
		double phaseDegrees)
	{
		ObjectDisposedException.ThrowIf(disposed,this);
		ArgumentNullException.ThrowIfNull(waveform);
		return requests.EnqueueOrdered(
			new(generator=>generator.UploadArbitraryWaveform(
				channel,
				waveform,
				frequencyHz,
				amplitudeVpp,
				offsetVolts,
				phaseDegrees))).Completion;
	}

	public async Task<ChannelSnapshot> ReadChannelAsync(int channel)
	{
		ObjectDisposedException.ThrowIf(disposed,this);
		await ioGate.WaitAsync(cancellation.Token);
		try
		{
			return await Task.Run(()=>client.ReadChannel(channel),cancellation.Token);
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
		requests.CancelAll();
		cancellation.Cancel();
		try
		{
			await worker;
		}
		catch(OperationCanceledException)
		{
		}
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

	private async Task ProcessWritesAsync()
	{
		while(!cancellation.IsCancellationRequested)
		{
			await requests.WaitAsync(cancellation.Token);
			while(requests.TryTakeNext(
				out ScheduledRequest<GeneratorWrite>? request))
			{
				try
				{
					await ioGate.WaitAsync(cancellation.Token);
					try
					{
						await Task.Run(
							()=>request.Value.Execute(client),
							cancellation.Token);
					}
					finally
					{
						ioGate.Release();
					}
					request.Succeed();
				}
				catch(OperationCanceledException)
				{
					request.Cancel();
					throw;
				}
				catch(Exception exception)
				{
					request.Fail(exception);
					CommunicationFailed?.Invoke(this,exception);
				}
			}
		}
	}

	private sealed record GeneratorWrite(
		Action<SiglentGeneratorClient> Execute);
}
