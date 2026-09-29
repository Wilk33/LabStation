using System.Text;
using Ka3005P.Core.Transport;

namespace Ka3005P.Tests.Fakes;

internal sealed class FakeSerialTransport : ISerialTransport
{
	private readonly Queue<byte[]> reads=new();
	private readonly List<string> writes=[];
	private byte[]? currentRead;
	private int currentReadOffset;
	private int activeOperations;
	private int maximumConcurrentOperations;

	public bool BlockReads { get; set; }
	public Exception? WriteException { get; set; }
	public int MaximumConcurrentOperations => Volatile.Read(ref maximumConcurrentOperations);
	public IReadOnlyList<string> WritesAsAscii => writes;

	public void QueueRead(byte[] value)
	{
		reads.Enqueue(value);
	}

	public void QueueZeroRead()
	{
		reads.Enqueue([]);
	}

	public ValueTask OpenAsync(string portName,CancellationToken cancellationToken)
	{
		return ValueTask.CompletedTask;
	}

	public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken cancellationToken)
	{
		EnterOperation();
		try
		{
			await Task.Yield();
			cancellationToken.ThrowIfCancellationRequested();
			if(WriteException is not null)
			{
				throw WriteException;
			}

			writes.Add(Encoding.ASCII.GetString(buffer.Span));
		}
		finally
		{
			ExitOperation();
		}
	}

	public async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken)
	{
		EnterOperation();
		try
		{
			if(BlockReads)
			{
				await Task.Delay(Timeout.InfiniteTimeSpan,cancellationToken);
			}

			await Task.Yield();
			cancellationToken.ThrowIfCancellationRequested();
			if(currentRead is null)
			{
				if(!reads.TryDequeue(out currentRead))
				{
					return 0;
				}

				currentReadOffset=0;
			}

			int remaining=currentRead.Length-currentReadOffset;
			if(remaining == 0)
			{
				currentRead=null;
				return 0;
			}

			int count=Math.Min(buffer.Length,remaining);
			currentRead.AsMemory(currentReadOffset,count).CopyTo(buffer);
			currentReadOffset+=count;
			if(currentReadOffset == currentRead.Length)
			{
				currentRead=null;
			}

			return count;
		}
		finally
		{
			ExitOperation();
		}
	}

	public ValueTask CloseAsync()
	{
		return ValueTask.CompletedTask;
	}

	public ValueTask DisposeAsync()
	{
		return ValueTask.CompletedTask;
	}

	private void EnterOperation()
	{
		int active=Interlocked.Increment(ref activeOperations);
		int observed=Volatile.Read(ref maximumConcurrentOperations);
		while(active > observed)
		{
			int exchanged=Interlocked.CompareExchange(
				ref maximumConcurrentOperations,
				active,
				observed);
			if(exchanged == observed)
			{
				break;
			}

			observed=exchanged;
		}
	}

	private void ExitOperation()
	{
		Interlocked.Decrement(ref activeOperations);
	}
}
