using System.Threading.Channels;

namespace Ka3005P.Core.Measurements;

public interface IMeasurementSink
{
	ValueTask WriteAsync(
		MeasurementSample sample,
		CancellationToken cancellationToken);
	ValueTask CompleteAsync(CancellationToken cancellationToken);
}

public sealed class MeasurementRecorder : IAsyncDisposable
{
	private readonly IMeasurementSink sink;
	private readonly int capacity;
	private readonly Channel<MeasurementSample> channel;
	private readonly Task writerTask;
	private readonly object stateGate=new();
	private readonly List<RecordingFailure> failures=[];
	private int outstanding;
	private RecordingState state=RecordingState.Running;
	private bool disposed;

	public MeasurementRecorder(IMeasurementSink sink,int capacity)
	{
		ArgumentNullException.ThrowIfNull(sink);
		if(capacity <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(capacity));
		}
		this.sink=sink;
		this.capacity=capacity;
		channel=Channel.CreateBounded<MeasurementSample>(
			new BoundedChannelOptions(capacity)
			{
				SingleReader=true,
				SingleWriter=false,
				FullMode=BoundedChannelFullMode.Wait
			});
		writerTask=WriteLoopAsync();
	}

	public event EventHandler<RecordingFailure>? RecordingFailed;

	public RecordingState State
	{
		get
		{
			lock(stateGate)
			{
				return state;
			}
		}
	}

	public IReadOnlyList<RecordingFailure> Failures
	{
		get
		{
			lock(stateGate)
			{
				return failures.ToArray();
			}
		}
	}

	public bool TryRecord(MeasurementSample sample)
	{
		lock(stateGate)
		{
			if(state != RecordingState.Running)
			{
				return false;
			}
			if(outstanding >= capacity)
			{
				FailOnceCore("Bufor zapisu pomiarów jest pełny.",null);
				channel.Writer.TryComplete();
				return false;
			}
			outstanding++;
		}

		if(channel.Writer.TryWrite(sample))
		{
			return true;
		}

		lock(stateGate)
		{
			outstanding--;
			FailOnceCore("Nie można zapisać próbki do bufora.",null);
		}
		return false;
	}

	public async ValueTask CompleteAsync(CancellationToken cancellationToken)
	{
		lock(stateGate)
		{
			if(state == RecordingState.Completed)
			{
				return;
			}
			if(state == RecordingState.Running)
			{
				state=RecordingState.Completing;
			}
			channel.Writer.TryComplete();
		}

		await writerTask.WaitAsync(cancellationToken).ConfigureAwait(false);
		if(State != RecordingState.Faulted)
		{
			await sink.CompleteAsync(cancellationToken).ConfigureAwait(false);
			lock(stateGate)
			{
				state=RecordingState.Completed;
			}
		}
	}

	public async ValueTask DisposeAsync()
	{
		if(disposed)
		{
			return;
		}
		disposed=true;
		await CompleteAsync(CancellationToken.None).ConfigureAwait(false);
	}

	private async Task WriteLoopAsync()
	{
		try
		{
			await foreach(
				MeasurementSample sample in channel.Reader.ReadAllAsync().ConfigureAwait(false))
			{
				try
				{
					await sink.WriteAsync(sample,CancellationToken.None).ConfigureAwait(false);
				}
				finally
				{
					lock(stateGate)
					{
						outstanding--;
					}
				}
			}
		}
		catch(Exception exception)
		{
			lock(stateGate)
			{
				FailOnceCore("Zapis pomiarów zakończył się błędem.",exception);
			}
			channel.Writer.TryComplete(exception);
		}
	}

	private void FailOnceCore(string message,Exception? exception)
	{
		if(state == RecordingState.Faulted)
		{
			return;
		}

		state=RecordingState.Faulted;
		RecordingFailure failure=new(DateTimeOffset.UtcNow,message,exception);
		failures.Add(failure);
		RecordingFailed?.Invoke(this,failure);
	}
}
