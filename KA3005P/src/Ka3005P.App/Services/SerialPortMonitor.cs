namespace Ka3005P.App.Services;

public delegate void SerialPortsChangedEventHandler(
	object? sender,
	IReadOnlyList<string> ports);

public interface ISerialPortMonitor : IAsyncDisposable
{
	event SerialPortsChangedEventHandler? PortsChanged;
	IReadOnlyList<string> Ports { get; }
	Exception? LastError { get; }
	void Start();
	ValueTask RefreshOnceAsync(CancellationToken cancellationToken);
}

public sealed class SerialPortMonitor : ISerialPortMonitor
{
	private readonly ISerialPortCatalog catalog;
	private readonly TimeSpan interval;
	private readonly CancellationTokenSource lifetime=new();
	private readonly object gate=new();
	private IReadOnlyList<string> ports=Array.Empty<string>();
	private Task? loop;
	private bool hasSnapshot;

	public SerialPortMonitor(ISerialPortCatalog catalog,TimeSpan interval)
	{
		ArgumentNullException.ThrowIfNull(catalog);
		if(interval <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(interval));
		}
		this.catalog=catalog;
		this.interval=interval;
	}

	public event SerialPortsChangedEventHandler? PortsChanged;

	public IReadOnlyList<string> Ports
	{
		get
		{
			lock(gate)
			{
				return ports;
			}
		}
	}

	public Exception? LastError { get; private set; }

	public void Start()
	{
		lock(gate)
		{
			loop??=Task.Run(RunAsync);
		}
	}

	public async ValueTask RefreshOnceAsync(CancellationToken cancellationToken)
	{
		IReadOnlyList<string> next;
		try
		{
			next=await Task.Run(
				()=>SystemSerialPortCatalog.Normalize(catalog.GetPortNames()),
				cancellationToken).ConfigureAwait(false);
		}
		catch(OperationCanceledException)
		{
			throw;
		}
		catch(Exception exception)
		{
			LastError=exception;
			return;
		}

		bool changed;
		lock(gate)
		{
			changed=!hasSnapshot || !ports.SequenceEqual(
				next,
				StringComparer.OrdinalIgnoreCase);
			ports=next;
			hasSnapshot=true;
			LastError=null;
		}
		if(changed)
		{
			PortsChanged?.Invoke(this,next);
		}
	}

	public async ValueTask DisposeAsync()
	{
		lifetime.Cancel();
		Task? current;
		lock(gate)
		{
			current=loop;
		}
		if(current is not null)
		{
			try
			{
				await current.ConfigureAwait(false);
			}
			catch(OperationCanceledException) when(lifetime.IsCancellationRequested)
			{
			}
		}
		lifetime.Dispose();
	}

	private async Task RunAsync()
	{
		while(!lifetime.IsCancellationRequested)
		{
			await RefreshOnceAsync(lifetime.Token).ConfigureAwait(false);
			await Task.Delay(interval,lifetime.Token).ConfigureAwait(false);
		}
	}
}
