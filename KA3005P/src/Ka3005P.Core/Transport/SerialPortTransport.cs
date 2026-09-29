using System.Diagnostics;
using System.IO.Ports;

namespace Ka3005P.Core.Transport;

public sealed class SerialPortTransport : ISerialTransport
{
	private readonly TimeSpan readTimeout;
	private readonly TimeSpan writeTimeout;
	private readonly SemaphoreSlim writeGate=new(1,1);
	private static readonly TimeSpan MinimumCommandInterval=TimeSpan.FromMilliseconds(45);
	private SerialPort? port;
	private long? lastWriteCompletedAt;

	public SerialPortTransport()
		: this(TimeSpan.FromMilliseconds(250),TimeSpan.FromMilliseconds(250))
	{
	}

	public SerialPortTransport(TimeSpan readTimeout,TimeSpan writeTimeout)
	{
		if(readTimeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(readTimeout));
		}
		if(writeTimeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(writeTimeout));
		}

		this.readTimeout=readTimeout;
		this.writeTimeout=writeTimeout;
	}

	public async ValueTask OpenAsync(string portName,CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(portName);
		if(port is not null)
		{
			throw new InvalidOperationException("Transport jest już otwarty.");
		}

		SerialPort created=new(portName,9600,Parity.None,8,StopBits.One)
		{
			Handshake=Handshake.None,
			DtrEnable=false,
			RtsEnable=false,
			ReadTimeout=ToMilliseconds(readTimeout),
			WriteTimeout=ToMilliseconds(writeTimeout)
		};

		try
		{
			await Task.Run(created.Open,cancellationToken).ConfigureAwait(false);
			port=created;
		}
		catch
		{
			created.Dispose();
			throw;
		}
	}

	public async ValueTask WriteAsync(
		ReadOnlyMemory<byte> buffer,
		CancellationToken cancellationToken)
	{
		SerialPort opened=GetOpenPort();
		byte[] bytes=buffer.ToArray();
		await writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if(lastWriteCompletedAt is long timestamp)
			{
				TimeSpan remaining=MinimumCommandInterval-
					Stopwatch.GetElapsedTime(timestamp);
				if(remaining > TimeSpan.Zero)
				{
					await Task.Delay(remaining,cancellationToken).ConfigureAwait(false);
				}
			}
			await Task.Run(
				()=>opened.Write(bytes,0,bytes.Length),
				cancellationToken).ConfigureAwait(false);
			lastWriteCompletedAt=Stopwatch.GetTimestamp();
		}
		catch(TimeoutException exception)
		{
			throw new IOException("Przekroczono limit czasu zapisu portu szeregowego.",exception);
		}
		finally
		{
			writeGate.Release();
		}
	}

	public async ValueTask<int> ReadAsync(
		Memory<byte> buffer,
		CancellationToken cancellationToken)
	{
		SerialPort opened=GetOpenPort();
		byte[] bytes=new byte[buffer.Length];
		try
		{
			int read=await Task.Run(
				()=>opened.Read(bytes,0,bytes.Length),
				cancellationToken).ConfigureAwait(false);
			bytes.AsMemory(0,read).CopyTo(buffer);
			return read;
		}
		catch(TimeoutException exception)
		{
			throw new IOException("Przekroczono limit czasu odczytu portu szeregowego.",exception);
		}
	}

	public ValueTask CloseAsync()
	{
		SerialPort? opened=port;
		port=null;
		lastWriteCompletedAt=null;
		if(opened is not null)
		{
			opened.Close();
			opened.Dispose();
		}

		return ValueTask.CompletedTask;
	}

	public async ValueTask DisposeAsync()
	{
		await CloseAsync().ConfigureAwait(false);
		writeGate.Dispose();
	}

	private SerialPort GetOpenPort()
	{
		if(port is null || !port.IsOpen)
		{
			throw new InvalidOperationException("Port szeregowy nie jest otwarty.");
		}

		return port;
	}

	private static int ToMilliseconds(TimeSpan timeout)
	{
		double milliseconds=Math.Ceiling(timeout.TotalMilliseconds);
		if(milliseconds > int.MaxValue)
		{
			return int.MaxValue;
		}

		return Math.Max(1,(int)milliseconds);
	}
}
