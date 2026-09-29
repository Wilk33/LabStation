using Ka3005P.App.ViewModels;
using Ka3005P.Core.Device;
using Ka3005P.Core.Dual;
using Ka3005P.Core.Sessions;
using Ka3005P.Tests.Fakes;

namespace Ka3005P.Tests.Integration;

internal sealed class TestApplication : IAsyncDisposable
{
	private readonly FakePowerSupplyDevice firstDevice;
	private readonly FakePowerSupplyDevice secondDevice;

	private TestApplication(
		FakePowerSupplyDevice firstDevice,
		FakePowerSupplyDevice secondDevice,
		PowerSupplySession firstSession,
		PowerSupplySession secondSession,
		DualSupplyViewModel viewModel)
	{
		this.firstDevice=firstDevice;
		this.secondDevice=secondDevice;
		FirstSession=firstSession;
		SecondSession=secondSession;
		ViewModel=viewModel;
	}

	public PowerSupplySession FirstSession { get; }
	public PowerSupplySession SecondSession { get; }
	public DualSupplyViewModel ViewModel { get; }

	public static async ValueTask<TestApplication> StartDualAsync(
		FakePowerSupplyDevice first,
		FakePowerSupplyDevice second)
	{
		PowerSupplySession firstSession=CreateSession(first);
		PowerSupplySession secondSession=CreateSession(second);
		await firstSession.StartAsync(CancellationToken.None);
		await secondSession.StartAsync(CancellationToken.None);
		DualPowerSupplyController controller=new(
			firstSession,
			secondSession,
			DualMode.Series);
		DualSupplyViewModel viewModel=new(controller);
		return new TestApplication(
			first,
			second,
			firstSession,
			secondSession,
			viewModel);
	}

	public Task WaitForBothVoltageWritesAsync(int hundredths)
	{
		return Task.WhenAll(
			firstDevice.WaitForVoltageAsync(hundredths),
			secondDevice.WaitForVoltageAsync(hundredths));
	}

	public async ValueTask DisposeAsync()
	{
		await ViewModel.CloseAsync(CancellationToken.None);
	}

	private static PowerSupplySession CreateSession(IPowerSupplyDevice device)
	{
		return new PowerSupplySession(
			device,
			TimeProvider.System,
			TimeSpan.FromHours(1));
	}
}
