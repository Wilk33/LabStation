using Ka3005P.Core.Dual;
using Ka3005P.Core.Measurements;
using Ka3005P.Core.Protocol;
using Ka3005P.Tests.Fakes;

namespace Ka3005P.Tests.Dual;

public sealed class DualPowerSupplyControllerTests
{
	[Fact]
	public void RequestSetpoints_PublishesPhysicalValuesToBothSessions()
	{
		FakePowerSupplySession first=new();
		FakePowerSupplySession second=new();
		DualPowerSupplyController controller=new(first,second,DualMode.Series);

		controller.RequestVoltage(VoltageSetpoint.FromHundredths(2501));
		controller.RequestCurrent(CurrentSetpoint.FromThousandths(1000));

		Assert.Equal([1250],first.Voltages);
		Assert.Equal([1251],second.Voltages);
		Assert.Equal([1000],first.Currents);
		Assert.Equal([1000],second.Currents);
	}

	[Fact]
	public async Task SetOutputAsync_RunsBothSessionsAndReportsPartialFailure()
	{
		FakePowerSupplySession first=new();
		FakePowerSupplySession second=new()
		{
			OutputFailure=new IOException("COM8")
		};
		DualPowerSupplyController controller=new(first,second,DualMode.Series);

		DualOperationResult result=
			await controller.SetOutputAsync(true,CancellationToken.None);

		Assert.True(first.OutputRequested);
		Assert.True(second.OutputRequested);
		Assert.True(result.IsPartialFailure);
		Assert.False(result.IsSuccess);
		Assert.Equal([true,false],first.Outputs);
		Assert.Equal([true,false],second.Outputs);
	}

	[Fact]
	public async Task SetOutputAsync_StartsSecondSessionWhileFirstIsBlocked()
	{
		FakePowerSupplySession first=new();
		FakePowerSupplySession second=new();
		first.BlockNextOutput();
		DualPowerSupplyController controller=new(first,second,DualMode.Series);

		Task<DualOperationResult> operation=
			controller.SetOutputAsync(true,CancellationToken.None).AsTask();
		await first.WaitUntilOutputStartsAsync();

		Assert.True(second.OutputRequested);
		first.ReleaseOutput();
		Assert.True((await operation).IsSuccess);
	}

	[Fact]
	public void Measurements_RequireFreshSampleFromBothSessions()
	{
		FakePowerSupplySession first=new();
		FakePowerSupplySession second=new();
		DualPowerSupplyController controller=new(first,second,DualMode.Parallel);
		List<DualMeasurement> received=[];
		controller.MeasurementReceived+=(_,measurement)=>received.Add(measurement);

		first.PublishMeasurement(new MeasurementSample(TimeSpan.FromSeconds(1),0,100));
		second.PublishMeasurement(new MeasurementSample(TimeSpan.FromSeconds(1),0,100));
		first.PublishMeasurement(new MeasurementSample(TimeSpan.FromSeconds(2),0,150));
		first.PublishMeasurement(new MeasurementSample(TimeSpan.FromSeconds(3),0,200));

		Assert.Single(received);
		second.PublishMeasurement(new MeasurementSample(TimeSpan.FromSeconds(3),0,200));

		Assert.Equal(2,received.Count);
		Assert.Equal(400,received[^1].CurrentThousandths);
		Assert.Equal(TimeSpan.FromSeconds(3),received[^1].First.Elapsed);
	}
}
