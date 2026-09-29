namespace Scope.Core;

public sealed class InstrumentOperationQueue : IDisposable

{

	private readonly SemaphoreSlim gate=new(1, 1);
	private int waitingCommands;
	public bool CommandPending => Volatile.Read(ref waitingCommands) > 0;

	public async Task RunCommandAsync(Func<Task> operation)

	{

		Interlocked.Increment(ref waitingCommands);
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
			Interlocked.Decrement(ref waitingCommands);
		}

	}

	public async Task<bool> TryRunPreviewAsync(Func<Task> operation)

	{

		if (CommandPending || !await gate.WaitAsync(0))
			return false;
		try

		{

			if (CommandPending)
				return false;
			await operation();
			return true;

		}
		finally
		{
			gate.Release();
		}

	}

	public void Dispose() => gate.Dispose();

}
