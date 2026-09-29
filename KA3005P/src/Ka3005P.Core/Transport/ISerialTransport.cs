namespace Ka3005P.Core.Transport;

public interface ISerialTransport : IAsyncDisposable
{
	ValueTask OpenAsync(string portName,CancellationToken cancellationToken);
	ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken cancellationToken);
	ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken);
	ValueTask CloseAsync();
}
