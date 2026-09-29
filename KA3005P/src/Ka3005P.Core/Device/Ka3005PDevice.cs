using Ka3005P.Core.Protocol;
using Ka3005P.Core.Transport;

namespace Ka3005P.Core.Device;

public sealed class Ka3005PDevice : IPowerSupplyDevice
{
	private readonly ISerialTransport transport;
	private readonly TimeSpan operationTimeout;
	private readonly SemaphoreSlim ioGate=new(1,1);
	private bool disposed;

	public Ka3005PDevice(ISerialTransport transport,TimeSpan operationTimeout)
	{
		ArgumentNullException.ThrowIfNull(transport);
		if(operationTimeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(operationTimeout));
		}

		this.transport=transport;
		this.operationTimeout=operationTimeout;
	}

	public ValueTask SetVoltageAsync(
		VoltageSetpoint value,
		CancellationToken cancellationToken)
	{
		byte[] command=Ka3005PCommandCodec.SetVoltage(value);
		return ExecuteSerializedAsync(
			ct=>transport.WriteAsync(command,ct),
			cancellationToken);
	}

	public ValueTask SetCurrentAsync(
		CurrentSetpoint value,
		CancellationToken cancellationToken)
	{
		byte[] command=Ka3005PCommandCodec.SetCurrent(value);
		return ExecuteSerializedAsync(
			ct=>transport.WriteAsync(command,ct),
			cancellationToken);
	}

	public ValueTask SetOutputAsync(bool enabled,CancellationToken cancellationToken)
	{
		byte[] command=Ka3005PCommandCodec.SetOutput(enabled);
		return ExecuteSerializedAsync(
			ct=>transport.WriteAsync(command,ct),
			cancellationToken);
	}

	public ValueTask<DeviceMeasurement> ReadMeasurementAsync(
		CancellationToken cancellationToken)
	{
		return ExecuteSerializedAsync(ReadMeasurementCoreAsync,cancellationToken);
	}

	public async ValueTask DisposeAsync()
	{
		if(disposed)
		{
			return;
		}

		disposed=true;
		await transport.DisposeAsync().ConfigureAwait(false);
		ioGate.Dispose();
	}

	private async ValueTask<DeviceMeasurement> ReadMeasurementCoreAsync(
		CancellationToken cancellationToken)
	{
		byte[] voltageReply=new byte[5];
		byte[] currentReply=new byte[5];

		await transport.WriteAsync(
			Ka3005PCommandCodec.ReadVoltage(),
			cancellationToken).ConfigureAwait(false);
		await ReadExactlyAsync(voltageReply,cancellationToken).ConfigureAwait(false);
		await transport.WriteAsync(
			Ka3005PCommandCodec.ReadCurrent(),
			cancellationToken).ConfigureAwait(false);
		await ReadExactlyAsync(currentReply,cancellationToken).ConfigureAwait(false);

		try
		{
			VoltageSetpoint voltage=Ka3005PCommandCodec.ParseVoltage(voltageReply);
			CurrentSetpoint current=Ka3005PCommandCodec.ParseCurrent(currentReply);
			return new DeviceMeasurement(voltage.Hundredths,current.Thousandths);
		}
		catch(ProtocolException exception)
		{
			throw new DeviceCommunicationException(
				"Urządzenie zwróciło nieprawidłową odpowiedź.",
				exception);
		}
	}

	private async ValueTask ReadExactlyAsync(
		Memory<byte> buffer,
		CancellationToken cancellationToken)
	{
		int offset=0;
		while(offset < buffer.Length)
		{
			int read=await transport.ReadAsync(
				buffer[offset..],
				cancellationToken).ConfigureAwait(false);
			if(read == 0)
			{
				throw new DeviceCommunicationException(
					"Port został zamknięty przed odebraniem pełnej odpowiedzi.");
			}

			offset+=read;
		}
	}

	private async ValueTask ExecuteSerializedAsync(
		Func<CancellationToken,ValueTask> operation,
		CancellationToken cancellationToken)
	{
		await ExecuteSerializedAsync(
			async ct=>
			{
				await operation(ct).ConfigureAwait(false);
				return true;
			},
			cancellationToken).ConfigureAwait(false);
	}

	private async ValueTask<T> ExecuteSerializedAsync<T>(
		Func<CancellationToken,ValueTask<T>> operation,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(disposed,this);
		using CancellationTokenSource timeout=
			CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(operationTimeout);
		bool entered=false;
		try
		{
			await ioGate.WaitAsync(timeout.Token).ConfigureAwait(false);
			entered=true;
			return await operation(timeout.Token).ConfigureAwait(false);
		}
		catch(OperationCanceledException exception) when(!cancellationToken.IsCancellationRequested)
		{
			throw new DeviceCommunicationException(
				"Przekroczono limit czasu komunikacji z urządzeniem.",
				exception);
		}
		catch(Exception exception) when(
			exception is IOException or
			UnauthorizedAccessException or
			InvalidOperationException)
		{
			throw new DeviceCommunicationException(
				"Błąd komunikacji z urządzeniem.",
				exception);
		}
		finally
		{
			if(entered)
			{
				ioGate.Release();
			}
		}
	}
}
