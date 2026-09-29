using Ka3005P.Core.Device;
using Ka3005P.Core.Dual;
using Ka3005P.Core.Protocol;
using Ka3005P.Core.Sessions;
using Ka3005P.Tests.Fakes;

namespace Ka3005P.Tests.Integration;

public sealed class CommandTrafficTests
{
	[Fact]
	public async Task RuntimeTraffic_NeverQueriesSetpointsOrStatus()
	{
		FakeSerialTransport firstTransport=CreateTransportWithMeasurement();
		FakeSerialTransport secondTransport=CreateTransportWithMeasurement();
		await using PowerSupplySession first=new(
			new Ka3005PDevice(firstTransport,TimeSpan.FromSeconds(1)),
			TimeProvider.System,
			TimeSpan.FromHours(1));
		await using PowerSupplySession second=new(
			new Ka3005PDevice(secondTransport,TimeSpan.FromSeconds(1)),
			TimeProvider.System,
			TimeSpan.FromHours(1));
		await first.StartAsync(CancellationToken.None);
		await second.StartAsync(CancellationToken.None);
		await using DualPowerSupplyController controller=new(
			first,
			second,
			DualMode.Series);
		controller.RequestVoltage(VoltageSetpoint.FromHundredths(1200));
		controller.RequestCurrent(CurrentSetpoint.FromThousandths(1000));
		await controller.SetOutputAsync(true,CancellationToken.None);
		first.RequestMeasurement();
		second.RequestMeasurement();
		await WaitUntilAsync(()=>
			firstTransport.WritesAsAscii.Contains("IOUT1?") &&
			secondTransport.WritesAsAscii.Contains("IOUT1?"));
		await controller.SetOutputAsync(false,CancellationToken.None);

		string[] traffic=[
			.. firstTransport.WritesAsAscii,
			.. secondTransport.WritesAsAscii];
		string[] forbidden=["VSET1?","ISET1?","STATUS?"];
		Assert.DoesNotContain(traffic,write=>forbidden.Contains(write));
		Assert.All(
			traffic.Where(write=>write.EndsWith('?')),
			query=>Assert.Contains(query,new[] { "VOUT1?","IOUT1?" }));
	}

	private static FakeSerialTransport CreateTransportWithMeasurement()
	{
		FakeSerialTransport transport=new();
		transport.QueueRead("12.00"u8.ToArray());
		transport.QueueRead("1.000"u8.ToArray());
		return transport;
	}

	private static async Task WaitUntilAsync(Func<bool> condition)
	{
		using CancellationTokenSource timeout=new(TimeSpan.FromSeconds(2));
		while(!condition())
		{
			await Task.Delay(10,timeout.Token);
		}
	}
}
