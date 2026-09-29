namespace Ka3005P.Core.Configuration;

public sealed record AppSettings
{
	public string? SinglePort { get; init; }
	public string? DualFirstPort { get; init; }
	public string? DualSecondPort { get; init; }
	public string? ExportDirectory { get; init; }
}
