using System.Diagnostics.CodeAnalysis;

namespace LabStation.Instruments.Scheduling;

public sealed class ScheduledRequest<T>
{
	private readonly TaskCompletionSource completion=
		new(TaskCreationOptions.RunContinuationsAsynchronously);

	internal ScheduledRequest(T value)
	{
		Value=value;
	}

	public T Value { get; }
	public Task Completion=>completion.Task;
	public void Succeed()=>completion.TrySetResult();
	public void Fail(Exception exception)=>completion.TrySetException(exception);
	public void Cancel()=>completion.TrySetCanceled();
}

public sealed class LatestRequestQueue<T> : IDisposable
{
	private readonly object gate=new();
	private readonly Queue<ScheduledRequest<T>> priority=[];
	private readonly LinkedList<(string Key,ScheduledRequest<T> Request)> latest=[];
	private readonly Queue<ScheduledRequest<T>> ordered=[];
	private readonly Dictionary<string,LinkedListNode<(string Key,ScheduledRequest<T> Request)>> byKey=[];
	private readonly SemaphoreSlim signal=new(0,1);
	private bool disposed;

	public int PendingCount
	{
		get
		{
			lock(gate)
			{
				return priority.Count+latest.Count+ordered.Count;
			}
		}
	}

	public ScheduledRequest<T> EnqueueLatest(string key,T value)
	{
		ScheduledRequest<T> request=new(value);
		lock(gate)
		{
			ObjectDisposedException.ThrowIf(disposed,this);
			if(byKey.TryGetValue(key,out LinkedListNode<(string Key,ScheduledRequest<T> Request)>? node))
			{
				node.Value.Request.Cancel();
				node.Value=(key,request);
			}
			else
			{
				byKey[key]=latest.AddLast((key,request));
			}
		}
		Signal();
		return request;
	}

	public ScheduledRequest<T> EnqueuePriority(T value)
	{
		ScheduledRequest<T> request=new(value);
		lock(gate)
		{
			ObjectDisposedException.ThrowIf(disposed,this);
			priority.Enqueue(request);
		}
		Signal();
		return request;
	}

	public ScheduledRequest<T> EnqueueOrdered(T value)
	{
		ScheduledRequest<T> request=new(value);
		lock(gate)
		{
			ObjectDisposedException.ThrowIf(disposed,this);
			ordered.Enqueue(request);
		}
		Signal();
		return request;
	}

	public ScheduledRequest<T> TakeNext()
	{
		if(!TryTakeNext(out ScheduledRequest<T>? request))
		{
			throw new InvalidOperationException("Kolejka jest pusta.");
		}
		return request;
	}

	public bool TryTakeNext([NotNullWhen(true)] out ScheduledRequest<T>? request)
	{
		lock(gate)
		{
			if(priority.TryDequeue(out request))
			{
				return true;
			}
			LinkedListNode<(string Key,ScheduledRequest<T> Request)>? node=latest.First;
			if(node is not null)
			{
				latest.RemoveFirst();
				byKey.Remove(node.Value.Key);
				request=node.Value.Request;
				return true;
			}
			return ordered.TryDequeue(out request);
		}
	}

	public ValueTask WaitAsync(CancellationToken cancellationToken)=>
		new(signal.WaitAsync(cancellationToken));

	public void CancelAll()
	{
		lock(gate)
		{
			while(priority.TryDequeue(out ScheduledRequest<T>? request))
			{
				request.Cancel();
			}
			foreach((string _,ScheduledRequest<T> request) in latest)
			{
				request.Cancel();
			}
			latest.Clear();
			byKey.Clear();
			while(ordered.TryDequeue(out ScheduledRequest<T>? request))
			{
				request.Cancel();
			}
		}
	}

	public void Dispose()
	{
		lock(gate)
		{
			if(disposed)
			{
				return;
			}
			disposed=true;
			CancelAll();
			signal.Dispose();
		}
	}

	private void Signal()
	{
		try
		{
			signal.Release();
		}
		catch(SemaphoreFullException)
		{
		}
	}
}
