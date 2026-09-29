using System.Diagnostics;
using Ka3005P.Tests.Fakes;

namespace Ka3005P.Tests.Integration;

public sealed class ResponsiveDualOperationTests
{
	[Fact]
	public async Task ActiveDualPolling_RapidSetpointChangesRemainNonBlocking()
	{
		FakePowerSupplyDevice first=new();
		FakePowerSupplyDevice second=new();
		await using TestApplication app=await TestApplication.StartDualAsync(
			first,
			second);
		await app.ViewModel.SetOutputAsync(true,CancellationToken.None);
		first.BlockNextMeasurement();
		second.BlockNextMeasurement();
		app.FirstSession.RequestMeasurement();
		app.SecondSession.RequestMeasurement();
		await Task.WhenAll(
			first.WaitUntilMeasurementStartsAsync(),
			second.WaitUntilMeasurementStartsAsync());

		Stopwatch stopwatch=Stopwatch.StartNew();
		for(int value=1200;value<=1250;value++)
		{
			app.ViewModel.SetVoltageFromHundredths(value);
		}
		stopwatch.Stop();

		Assert.Equal("12,50",app.ViewModel.VoltageText);
		Assert.True(stopwatch.Elapsed<TimeSpan.FromMilliseconds(100));
		Assert.True(app.FirstSession.PendingSetpointCount<=2);
		Assert.True(app.SecondSession.PendingSetpointCount<=2);
		first.ReleaseOperation();
		second.ReleaseOperation();
		await app.WaitForBothVoltageWritesAsync(625);
	}
}
