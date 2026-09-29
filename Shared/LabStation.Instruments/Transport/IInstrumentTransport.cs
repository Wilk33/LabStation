namespace LabStation.Instruments.Transport;

public interface IInstrumentTransport : IDisposable
{
	void Write(string command);
	byte[] Query(string command);
}
