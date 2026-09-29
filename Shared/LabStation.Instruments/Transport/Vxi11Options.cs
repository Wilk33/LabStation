namespace LabStation.Instruments.Transport;

public sealed record Vxi11Options
{
	public string DeviceName { get;init; }="inst0";
	public TimeSpan ConnectTimeout { get;init; }=TimeSpan.FromSeconds(5);
	public TimeSpan IoTimeout { get;init; }=TimeSpan.FromSeconds(15);
	public int MaximumResponseBytes { get;init; }=32*1024*1024;
	public string CommandTerminator { get;init; }="\n";
}
