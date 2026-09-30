namespace LabStation.Instruments.Transport;

public interface IRawInstrumentTransport
{
	void Write(ReadOnlyMemory<byte> data);
}
