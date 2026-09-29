using Ka3005P.Core.Device;
using Ka3005P.Core.Measurements;
using Ka3005P.Core.Protocol;
using Ka3005P.Core.Sessions;
using Ka3005P.Tests.Fakes;

namespace Ka3005P.Tests.Sessions;

public sealed class MeasurementLoopTests
{
	[Fact]
	public async Task Polling_StopsAfterOutputOffCompletes()
	{
		ManualTimeProvider time=new();
		FakePowerSupplyDevice device=new();
		await using PowerSupplySession session=
			new(device,time,TimeSpan.FromMilliseconds(100));
		await session.StartAsync(CancellationToken.None);

		time.Advance(TimeSpan.FromSeconds(1));
		Assert.Equal(0,device.MeasurementReads);
		await time.WaitForTimerCountAsync(2);

		await session.SetOutputAsync(true,CancellationToken.None);
		device.BlockNextMeasurement();
		time.Advance(TimeSpan.FromSeconds(1));
		await device.WaitUntilMeasurementStartsAsync();
		time.Advance(TimeSpan.FromSeconds(5));

		Assert.Equal(1,device.MeasurementReads);
		device.ReleaseOperation();
		await session.SetOutputAsync(false,CancellationToken.None);
		int readsAfterOff=device.MeasurementReads;
		time.Advance(TimeSpan.FromSeconds(1));
		Assert.Equal(readsAfterOff,device.MeasurementReads);
	}

	[Fact]
	public async Task ZeroOutputMeasurement_DoesNotOverwriteSetpoints()
	{
		ManualTimeProvider time=new();
		FakePowerSupplyDevice device=new()
		{
			Measurement=new DeviceMeasurement(0,0)
		};
		await using PowerSupplySession session=
			new(device,time,TimeSpan.FromMilliseconds(100));
		await session.StartAsync(CancellationToken.None);
		session.RequestVoltage(VoltageSetpoint.FromHundredths(1200));
		session.RequestCurrent(CurrentSetpoint.FromThousandths(1000));
		await device.WaitForVoltageAsync(1200);
		await session.SetOutputAsync(true,CancellationToken.None);

		time.Advance(TimeSpan.FromSeconds(1));
		await device.WaitForMeasurementReadsAsync(1);
		await session.SetOutputAsync(false,CancellationToken.None);

		SessionSnapshot snapshot=session.Snapshot;
		Assert.Equal(1200,snapshot.RequestedVoltage?.Hundredths);
		Assert.Equal(1200,snapshot.SentVoltage?.Hundredths);
		Assert.Equal(1000,snapshot.RequestedCurrent?.Thousandths);
		Assert.Equal(1000,snapshot.SentCurrent?.Thousandths);
		Assert.Equal(0,snapshot.LastMeasurement?.VoltageHundredths);
		Assert.Equal(0,snapshot.LastMeasurement?.CurrentThousandths);
	}

	[Fact]
	public async Task MeasurementReceived_UsesMonotonicTimestampAndSessionElapsedTime()
	{
		ManualTimeProvider time=new();
		FakePowerSupplyDevice device=new();
		await using PowerSupplySession session=
			new(device,time,TimeSpan.FromMilliseconds(100));
		TaskCompletionSource<MeasurementSample> received=
			new(TaskCreationOptions.RunContinuationsAsynchronously);
		session.MeasurementReceived+=(_,sample)=>received.TrySetResult(sample);
		await session.StartAsync(CancellationToken.None);
		await session.SetOutputAsync(true,CancellationToken.None);

		time.Advance(TimeSpan.FromMilliseconds(250));
		MeasurementSample sample=
			await received.Task.WaitAsync(TimeSpan.FromSeconds(2));

		Assert.Equal(TimeSpan.FromMilliseconds(250).Ticks,sample.Timestamp);
		Assert.Equal(TimeSpan.FromMilliseconds(250),sample.Elapsed);
		Assert.Equal(TimeSpan.Zero,session.Snapshot.MeasurementAge);
	}

	[Fact]
	public async Task SlowMeasurement_DoesNotAddAnotherFullIntervalAfterCompletion()
	{
		ManualTimeProvider time=new();
		FakePowerSupplyDevice device=new();
		await using PowerSupplySession session=
			new(device,time,TimeSpan.FromMilliseconds(100));
		await session.StartAsync(CancellationToken.None);
		await session.SetOutputAsync(true,CancellationToken.None);
		device.BlockNextMeasurement();

		time.Advance(TimeSpan.FromMilliseconds(100));
		await device.WaitUntilMeasurementStartsAsync();
		time.Advance(TimeSpan.FromMilliseconds(150));
		device.ReleaseOperation();

		await device.WaitForMeasurementReadsAsync(2);
		Assert.Equal(2,device.MeasurementReads);
	}
}
