namespace LabStation.Instruments.Scheduling;

public sealed class SerializedOperationGate : IDisposable
{
	private readonly SemaphoreSlim gate=new(1,1);
	private int waitingForeground;

	public bool ForegroundPending=>Volatile.Read(ref waitingForeground)>0;

	public async Task RunForegroundAsync(Func<Task> operation)
	{
		Interlocked.Increment(ref waitingForeground);
		try
		{
			await gate.WaitAsync();
			try
			{
				await operation();
			}
			finally
			{
				gate.Release();
			}
		}
		finally
		{
			Interlocked.Decrement(ref waitingForeground);
		}
	}

	public async Task<T> RunForegroundAsync<T>(Func<Task<T>> operation)
	{
		T? result=default;
		await RunForegroundAsync(async()=>result=await operation());
		return result!;
	}

	public async Task<bool> TryRunBackgroundAsync(Func<Task> operation)
	{
		if(ForegroundPending || !await gate.WaitAsync(0))
		{
			return false;
		}
		try
		{
			if(ForegroundPending)
			{
				return false;
			}
			await operation();
			return true;
		}
		finally
		{
			gate.Release();
		}
	}

	public void Dispose()=>gate.Dispose();
}
