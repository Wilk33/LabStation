using Ka3005P.App.Services;
using Ka3005P.App.ViewModels;
using Ka3005P.Core.Measurements;
using Ka3005P.Tests.Fakes;

namespace Ka3005P.Tests.ViewModels;

public sealed class ExportCommandTests : IDisposable
{
	private readonly string directory=Path.Combine(
		Path.GetTempPath(),
		$"ka3005p-export-{Guid.NewGuid():N}");

	[Fact]
	public async Task SaveVoltageCommand_WritesCurrentSessionAsCsv()
	{
		Directory.CreateDirectory(directory);
		string path=Path.Combine(directory,"voltage.csv");
		FakePowerSupplySession session=new();
		SingleSupplyViewModel main=new(session);
		using ChartViewModel chart=main.CreateChartViewModel(
			new FakeFileDialogService(path));
		session.PublishMeasurement(
			new MeasurementSample(TimeSpan.FromMilliseconds(150),1234,123));

		await chart.SaveVoltageCommand.ExecuteAsync(null);

		string content=await File.ReadAllTextAsync(path);
		Assert.Contains("Time;Voltage;",content);
		Assert.Contains("0.150;12.34;",content);
	}

	[Fact]
	public async Task SaveVoltageAndCurrentCommand_WritesBothMeasurementsAsCsv()
	{
		Directory.CreateDirectory(directory);
		string path=Path.Combine(directory,"voltage-current.csv");
		FakePowerSupplySession session=new();
		SingleSupplyViewModel main=new(session);
		using ChartViewModel chart=main.CreateChartViewModel(
			new FakeFileDialogService(path));
		session.PublishMeasurement(
			new MeasurementSample(TimeSpan.FromMilliseconds(1250),1234,567));

		await chart.SaveVoltageAndCurrentCommand.ExecuteAsync(null);

		string content=await File.ReadAllTextAsync(path);
		Assert.Contains("Time;Voltage;Current;",content);
		Assert.Contains("1.250;12.34;0.567;",content);
	}

	public void Dispose()
	{
		if(Directory.Exists(directory))
		{
			Directory.Delete(directory,true);
		}
	}
}

internal sealed class FakeFileDialogService : IFileDialogService
{
	private readonly string? path;

	public FakeFileDialogService(string? path=null)
	{
		this.path=path;
	}

	public ValueTask<string?> ChooseSavePathAsync(
		string suggestedFileName,
		CancellationToken cancellationToken)
	{
		return ValueTask.FromResult(path);
	}
}
