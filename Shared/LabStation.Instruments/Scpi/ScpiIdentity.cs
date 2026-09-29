namespace LabStation.Instruments.Scpi;

public sealed record ScpiIdentity(
	string Manufacturer,
	string Model,
	string SerialNumber,
	string Firmware)
{
	public static ScpiIdentity Parse(string response)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(response);
		string[] parts=response.Split(',',StringSplitOptions.TrimEntries);
		if(parts.Length<2 || parts[0].Length == 0 || parts[1].Length == 0)
		{
			throw new InvalidDataException("Nieprawidłowa odpowiedź *IDN?: "+response);
		}
		return new ScpiIdentity(
			parts[0],
			parts[1],
			parts.Length>2 ? parts[2] : string.Empty,
			parts.Length>3 ? string.Join(',',parts[3..]) : string.Empty);
	}
}
