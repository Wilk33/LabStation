namespace Ka3005P.Tests.Fakes;

internal sealed class ManualTimeProvider : TimeProvider
{
	private readonly object gate=new();
	private readonly List<ManualTimer> timers=[];
	private DateTimeOffset utcNow=new(2026,9,24,8,0,0,TimeSpan.Zero);
	private long timestamp;
	private int createdTimerCount;
	private TaskCompletionSource timerCreated=NewSignal();

	public override long TimestampFrequency => TimeSpan.TicksPerSecond;

	public override DateTimeOffset GetUtcNow()
	{
		lock(gate)
		{
			return utcNow;
		}
	}

	public override long GetTimestamp()
	{
		lock(gate)
		{
			return timestamp;
		}
	}

	public override ITimer CreateTimer(
		TimerCallback callback,
		object? state,
		TimeSpan dueTime,
		TimeSpan period)
	{
		ArgumentNullException.ThrowIfNull(callback);
		ManualTimer timer;
		lock(gate)
		{
			timer=new ManualTimer(this,callback,state,dueTime,period,timestamp);
			timers.Add(timer);
			createdTimerCount++;
			TaskCompletionSource signal=timerCreated;
			timerCreated=NewSignal();
			signal.TrySetResult();
		}
		return timer;
	}

	public async Task WaitForTimerCountAsync(int expected)
	{
		while(true)
		{
			Task signal;
			lock(gate)
			{
				if(createdTimerCount>=expected)
				{
					return;
				}
				signal=timerCreated.Task;
			}
			await signal.WaitAsync(TimeSpan.FromSeconds(2));
		}
	}

	public void Advance(TimeSpan duration)
	{
		if(duration < TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(duration));
		}

		ManualTimer[] due;
		lock(gate)
		{
			utcNow+=duration;
			timestamp+=duration.Ticks;
			due=timers.Where(timer=>timer.IsDue(timestamp)).ToArray();
		}

		foreach(ManualTimer timer in due)
		{
			timer.Fire(timestamp);
		}
	}

	private void Remove(ManualTimer timer)
	{
		lock(gate)
		{
			timers.Remove(timer);
		}
	}

	private static TaskCompletionSource NewSignal()
	{
		return new(TaskCreationOptions.RunContinuationsAsynchronously);
	}

	private sealed class ManualTimer : ITimer
	{
		private readonly ManualTimeProvider owner;
		private readonly TimerCallback callback;
		private readonly object? state;
		private readonly object gate=new();
		private long dueTimestamp;
		private long periodTicks;
		private bool disposed;

		public ManualTimer(
			ManualTimeProvider owner,
			TimerCallback callback,
			object? state,
			TimeSpan dueTime,
			TimeSpan period,
			long now)
		{
			this.owner=owner;
			this.callback=callback;
			this.state=state;
			ChangeCore(dueTime,period,now);
		}

		public bool Change(TimeSpan dueTime,TimeSpan period)
		{
			lock(gate)
			{
				if(disposed)
				{
					return false;
				}

				ChangeCore(dueTime,period,owner.GetTimestamp());
				return true;
			}
		}

		public void Dispose()
		{
			lock(gate)
			{
				disposed=true;
			}
			owner.Remove(this);
		}

		public ValueTask DisposeAsync()
		{
			Dispose();
			return ValueTask.CompletedTask;
		}

		public bool IsDue(long now)
		{
			lock(gate)
			{
				return !disposed && dueTimestamp != long.MaxValue && dueTimestamp <= now;
			}
		}

		public void Fire(long now)
		{
			lock(gate)
			{
				if(disposed || dueTimestamp == long.MaxValue || dueTimestamp > now)
				{
					return;
				}

				dueTimestamp=periodTicks == Timeout.InfiniteTimeSpan.Ticks
					? long.MaxValue
					: now+periodTicks;
			}

			callback(state);
		}

		private void ChangeCore(TimeSpan dueTime,TimeSpan period,long now)
		{
			dueTimestamp=dueTime == Timeout.InfiniteTimeSpan
				? long.MaxValue
				: now+dueTime.Ticks;
			periodTicks=period == Timeout.InfiniteTimeSpan
				? Timeout.InfiniteTimeSpan.Ticks
				: period.Ticks;
		}
	}
}
