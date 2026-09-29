using Ka3005P.Core.Device;
using Ka3005P.Core.Protocol;
using Ka3005P.Tests.Fakes;

namespace Ka3005P.Tests.Device;

public sealed class Ka3005PDeviceTests
{
	[Fact]
	public async Task ReadMeasurementAsync_CombinesPartialReadsWithoutConcurrentIo()
	{
		FakeSerialTransport transport=new();
		transport.QueueRead("12"u8.ToArray());
		transport.QueueRead(".34"u8.ToArray());
		transport.QueueRead("0.123"u8.ToArray());
		Ka3005PDevice device=new(transport,TimeSpan.FromMilliseconds(200));

		DeviceMeasurement result=await device.ReadMeasurementAsync(CancellationToken.None);

		Assert.Equal(1234,result.VoltageHundredths);
		Assert.Equal(123,result.CurrentThousandths);
		Assert.Equal(1,transport.MaximumConcurrentOperations);
		Assert.Equal(["VOUT1?","IOUT1?"],transport.WritesAsAscii);
	}

	[Fact]
	public async Task ReadMeasurementAsync_TimesOutBeforeFiveBytes()
	{
		FakeSerialTransport transport=new(){BlockReads=true};
		Ka3005PDevice device=new(transport,TimeSpan.FromMilliseconds(50));

		DeviceCommunicationException exception=await Assert.ThrowsAsync<DeviceCommunicationException>(
			()=>device.ReadMeasurementAsync(CancellationToken.None).AsTask());

		Assert.Contains("limit czasu",exception.Message,StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task ReadMeasurementAsync_RejectsZeroByteRead()
	{
		FakeSerialTransport transport=new();
		transport.QueueZeroRead();
		Ka3005PDevice device=new(transport,TimeSpan.FromMilliseconds(200));

		DeviceCommunicationException exception=await Assert.ThrowsAsync<DeviceCommunicationException>(
			()=>device.ReadMeasurementAsync(CancellationToken.None).AsTask());

		Assert.Contains("zamknięty",exception.Message,StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task SetVoltageAsync_WrapsTransportWriteFailure()
	{
		FakeSerialTransport transport=new(){WriteException=new IOException("write failed")};
		Ka3005PDevice device=new(transport,TimeSpan.FromMilliseconds(200));

		DeviceCommunicationException exception=await Assert.ThrowsAsync<DeviceCommunicationException>(
			()=>device.SetVoltageAsync(VoltageSetpoint.FromHundredths(1200),CancellationToken.None).AsTask());

		Assert.IsType<IOException>(exception.InnerException);
	}

	[Fact]
	public async Task Setters_WriteOnlyCommandsWithoutReadbackQueries()
	{
		FakeSerialTransport transport=new();
		Ka3005PDevice device=new(transport,TimeSpan.FromMilliseconds(200));

		await device.SetVoltageAsync(VoltageSetpoint.FromHundredths(1200),CancellationToken.None);
		await device.SetCurrentAsync(CurrentSetpoint.FromThousandths(1000),CancellationToken.None);
		await device.SetOutputAsync(true,CancellationToken.None);
		await device.SetOutputAsync(false,CancellationToken.None);

		Assert.Equal(["VSET1:12.00","ISET1:1.000","OUT1","OUT0"],transport.WritesAsAscii);
		Assert.DoesNotContain(transport.WritesAsAscii,value=>value is "VSET1?" or "ISET1?" or "STATUS?");
	}

	[Fact]
	public async Task ReadMeasurementAsync_PreservesCallerCancellation()
	{
		FakeSerialTransport transport=new(){BlockReads=true};
		Ka3005PDevice device=new(transport,TimeSpan.FromSeconds(5));
		using CancellationTokenSource cancellation=new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			()=>device.ReadMeasurementAsync(cancellation.Token).AsTask());
	}
}
