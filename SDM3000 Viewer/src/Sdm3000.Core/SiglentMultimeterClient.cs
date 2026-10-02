using LabStation.Instruments.Scpi;
using LabStation.Instruments.Transport;

namespace Sdm3000.Core;

public sealed class SiglentMultimeterClient : IDisposable
{
	private readonly ScpiConnection connection;

	public SiglentMultimeterClient(IInstrumentTransport transport)
	{
		connection=new(transport);
	}

	public ScpiIdentity Initialize()
	{
		ScpiIdentity identity=connection.Identify();
		if(!IsSupported(identity))
		{
			throw new InvalidDataException(
				"Ta wersja aplikacji obsługuje multimetry SIGLENT SDM3000. Odpowiedź: "+
				string.Join(',',identity.Manufacturer,identity.Model,identity.SerialNumber,identity.Firmware));
		}
		return identity;
	}

	public static bool IsSupported(ScpiIdentity identity)
	{
		return identity.Manufacturer.Contains(
			"SIGLENT",
			StringComparison.OrdinalIgnoreCase) &&
			identity.Model.StartsWith(
				"SDM3",
				StringComparison.OrdinalIgnoreCase);
	}

	public MeasurementSnapshot ReadSnapshot(MeasurementAccumulator accumulator)
	{
		ArgumentNullException.ThrowIfNull(accumulator);
		MeasurementConfiguration configuration=MultimeterProtocol.ParseConfiguration(
			connection.QueryText("CONFigure?"));
		MeasurementReading reading=MultimeterProtocol.ParseReading(
			connection.QueryText("DATA:LAST?"));
		long storedPoints=MultimeterProtocol.ParsePointCount(
			connection.QueryText("DATA:POINts?"));
		return accumulator.Accept(configuration,reading,storedPoints);
	}

	public void Dispose()
	{
		connection.Dispose();
	}
}
