using Ka3005P.App.Services;
using Ka3005P.Core.Configuration;

namespace Ka3005P.App.ViewModels;

public enum ApplicationMode
{
	Single,
	Dual
}

public interface ISupplyModeViewModel :
	IOutputController,
	IChartSampleSource,
	IMeasurementExporter
{
	ApplicationMode ApplicationMode { get; }
	string? PrimaryPort { get; }
	string? SecondaryPort { get; }
	void UpdateAvailablePorts(IReadOnlyList<string> ports);
	ChartViewModel CreateChartViewModel(IFileDialogService fileDialog);
	ValueTask CloseAsync(CancellationToken cancellationToken);
}

public interface ISupplyModeFactory
{
	ISupplyModeViewModel Create(
		ApplicationMode mode,
		IReadOnlyList<string> ports,
		AppSettings settings);
}
