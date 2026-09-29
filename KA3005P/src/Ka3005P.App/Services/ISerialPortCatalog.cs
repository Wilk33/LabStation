namespace Ka3005P.App.Services;

public interface ISerialPortCatalog
{
	IReadOnlyList<string> GetPortNames();
}
