using Ka3005P.Core.Protocol;
using Ka3005P.Core.Sessions;

namespace Ka3005P.Tests.Sessions;

public sealed class SessionRequestQueueTests
{
	[Fact]
	public void EnqueueVoltage_ReplacesOlderPendingVoltage()
	{
		SessionRequestQueue queue=new();
		queue.SetVoltage(VoltageSetpoint.FromHundredths(1200));
		queue.SetVoltage(VoltageSetpoint.FromHundredths(1250));

		SessionRequest request=queue.TakeNext();

		SetVoltageRequest voltage=Assert.IsType<SetVoltageRequest>(request);
		Assert.Equal(1250,voltage.Value.Hundredths);
		Assert.False(queue.TryTakeNext(out _));
	}

	[Fact]
	public void VoltageAndCurrent_HaveIndependentPendingSlots()
	{
		SessionRequestQueue queue=new();
		queue.SetVoltage(VoltageSetpoint.FromHundredths(1200));
		queue.SetCurrent(CurrentSetpoint.FromThousandths(1000));

		SetVoltageRequest voltage=Assert.IsType<SetVoltageRequest>(queue.TakeNext());
		SetCurrentRequest current=Assert.IsType<SetCurrentRequest>(queue.TakeNext());

		Assert.Equal(1200,voltage.Value.Hundredths);
		Assert.Equal(1000,current.Value.Thousandths);
		Assert.False(queue.TryTakeNext(out _));
	}

	[Fact]
	public void OutputOff_PreemptsAndDiscardsPendingSetpoints()
	{
		SessionRequestQueue queue=new();
		queue.SetVoltage(VoltageSetpoint.FromHundredths(1200));
		queue.SetCurrent(CurrentSetpoint.FromThousandths(1000));
		queue.SetOutput(false);

		SetOutputRequest output=Assert.IsType<SetOutputRequest>(queue.TakeNext());

		Assert.False(output.Enabled);
		Assert.False(queue.TryTakeNext(out _));
	}

	[Fact]
	public void Close_PreemptsAndDiscardsEverything()
	{
		SessionRequestQueue queue=new();
		queue.SetVoltage(VoltageSetpoint.FromHundredths(1200));
		queue.SetOutput(true);
		queue.Close();

		Assert.IsType<CloseSessionRequest>(queue.TakeNext());
		Assert.False(queue.TryTakeNext(out _));
	}

	[Fact]
	public void RepeatedOutputOff_RemainsSinglePendingRequest()
	{
		SessionRequestQueue queue=new();
		for(int index=0;index<100;index++)
		{
			queue.SetOutput(false);
		}

		Assert.Equal(1,queue.PendingRequestCount);
		Assert.IsType<SetOutputRequest>(queue.TakeNext());
		Assert.False(queue.TryTakeNext(out _));
	}
}
