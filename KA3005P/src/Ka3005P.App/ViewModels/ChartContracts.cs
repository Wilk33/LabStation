using Ka3005P.Core.Measurements;

namespace Ka3005P.App.ViewModels;

public interface IOutputController
{
	event EventHandler<bool>? OutputStateChanged;
	event EventHandler<bool>? ConnectionStateChanged;
	bool IsOutputOn { get; }
	bool IsConnected { get; }
	ValueTask SetOutputAsync(bool enabled,CancellationToken cancellationToken);
}

public readonly record struct ChartSample(
	TimeSpan Elapsed,
	int VoltageHundredths,
	int CurrentThousandths,
	bool IsCurrentLimited);

public interface IChartSampleSource
{
	event EventHandler<ChartSample>? ChartSampleReceived;
}

public interface IMeasurementExporter
{
	ValueTask ExportAsync(
		MeasurementExportKind kind,
		string path,
		CancellationToken cancellationToken);
}
