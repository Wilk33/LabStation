using Ka3005P.Core.Protocol;
using Ka3005P.Core.Sessions;
using Ka3005P.Tests.Fakes;

namespace Ka3005P.Tests.Sessions;

public sealed class PowerSupplySessionTests
{
	[Fact]
	public async Task RapidChanges_DoNotWaitForDevice_AndSendLatestPendingValue()
	{
		FakePowerSupplyDevice device=new();
		device.BlockNextOperation();
		await using PowerSupplySession session=new(device,TimeProvider.System);
		await session.StartAsync(CancellationToken.None);

		session.RequestVoltage(VoltageSetpoint.FromHundredths(1200));
		await device.WaitUntilOperationStartsAsync();
		for(int value=1201;value<=1250;value++)
		{
			session.RequestVoltage(VoltageSetpoint.FromHundredths(value));
		}
		device.ReleaseOperation();
		await device.WaitForVoltageAsync(1250);

		Assert.Equal([1200,1250],device.VoltageWrites);
		Assert.Equal(1,device.MaximumConcurrentOperations);
	}

	[Fact]
	public async Task OutputOff_AfterSlowMeasurement_DiscardsPendingSetpoint()
	{
		FakePowerSupplyDevice device=new();
		device.BlockNextOperation();
		await using PowerSupplySession session=new(device,TimeProvider.System);
		await session.StartAsync(CancellationToken.None);

		session.RequestMeasurement();
		await device.WaitUntilOperationStartsAsync();
		session.RequestVoltage(VoltageSetpoint.FromHundredths(1200));
		ValueTask outputTask=session.SetOutputAsync(false,CancellationToken.None);
		device.ReleaseOperation();
		await outputTask;
		await device.WaitForOutputAsync(false);

		Assert.Equal(["Measurement","Output:False"],device.Operations);
		Assert.Empty(device.VoltageWrites);
	}

	[Fact]
	public async Task SuccessfulWrite_UpdatesSentSnapshotWithoutReadback()
	{
		FakePowerSupplyDevice device=new();
		await using PowerSupplySession session=new(device,TimeProvider.System);
		await session.StartAsync(CancellationToken.None);

		session.RequestVoltage(VoltageSetpoint.FromHundredths(1234));
		await device.WaitForVoltageAsync(1234);

		Assert.Equal(1234,session.Snapshot.RequestedVoltage?.Hundredths);
		Assert.Equal(1234,session.Snapshot.SentVoltage?.Hundredths);
		Assert.Null(session.Snapshot.Error);
	}
}
