using Ka3005P.App.Services;
using Ka3005P.App.ViewModels;
using Ka3005P.Core.Configuration;
using Ka3005P.Core.Device;
using Ka3005P.Core.Dual;
using Ka3005P.Core.Measurements;
using Ka3005P.Core.Protocol;
using Ka3005P.Core.Sessions;
using Ka3005P.Tests.Fakes;
using System.Collections.Specialized;

namespace Ka3005P.Tests.ViewModels;

public sealed class DualSupplyViewModelTests
{
	[Fact]
	public void AvailablePorts_SelectTwoDistinctPortsAndFilterOppositeChoice()
	{
		DualSupplyViewModel viewModel=new(
			new PortLeaseRegistry(),
			new FakeSessionFactory(),
			["COM5","COM6","COM7"],
			"COM5",
			"COM6");

		Assert.Equal("COM5",viewModel.SelectedFirstPort);
		Assert.Equal("COM6",viewModel.SelectedSecondPort);
		Assert.DoesNotContain("COM5",viewModel.AvailableSecondPorts);
		Assert.DoesNotContain("COM6",viewModel.AvailableFirstPorts);
		Assert.True(viewModel.ConnectCommand.CanExecute(null));
	}

	[Fact]
	public void SelectingCom5AndCom6_PreservesBothSelectionsAfterListRebuild()
	{
		DualSupplyViewModel viewModel=new(
			new PortLeaseRegistry(),
			new FakeSessionFactory(),
			["COM1","COM2","COM3","COM4","COM5","COM6"]);
		bool collectionWasReset=false;
		viewModel.AvailableFirstPorts.CollectionChanged+=(_,eventArgs)=>
			collectionWasReset|=eventArgs.Action == NotifyCollectionChangedAction.Reset;
		viewModel.AvailableSecondPorts.CollectionChanged+=(_,eventArgs)=>
			collectionWasReset|=eventArgs.Action == NotifyCollectionChangedAction.Reset;

		viewModel.SelectedFirstPort="COM5";
		viewModel.SelectedSecondPort="COM6";

		Assert.Equal("COM5",viewModel.SelectedFirstPort);
		Assert.Equal("COM6",viewModel.SelectedSecondPort);
		Assert.DoesNotContain("COM6",viewModel.AvailableFirstPorts);
		Assert.DoesNotContain("COM5",viewModel.AvailableSecondPorts);
		Assert.True(viewModel.ConnectCommand.CanExecute(null));
		Assert.False(collectionWasReset);
	}

	[Fact]
	public void OneAvailablePort_DisablesDualConnect()
	{
		DualSupplyViewModel viewModel=new(
			new PortLeaseRegistry(),
			new FakeSessionFactory(),
			["COM5"]);

		Assert.Equal("COM5",viewModel.SelectedFirstPort);
		Assert.Null(viewModel.SelectedSecondPort);
		Assert.False(viewModel.ConnectCommand.CanExecute(null));
	}

	[Theory]
	[InlineData(DualMode.Series,"62,00","5,100")]
	[InlineData(DualMode.Parallel,"31,00","10,200")]
	[InlineData(DualMode.Symmetric,"31,00","5,100")]
	public void ChangeMode_UpdatesLimitsAndRevalidatesSetpoints(
		DualMode mode,
		string maxVoltage,
		string maxCurrent)
	{
		(DualSupplyViewModel viewModel,_,_)=CreateViewModel();

		viewModel.Mode=mode;

		Assert.Equal(maxVoltage,viewModel.MaximumVoltageText);
		Assert.Equal(maxCurrent,viewModel.MaximumCurrentText);
		Assert.True(viewModel.SetpointsAreValid);
	}

	[Fact]
	public async Task Connect_EnablesOutputCommand()
	{
		DualSupplyViewModel viewModel=new(
			new PortLeaseRegistry(),
			new FakeSessionFactory());
		int changes=0;
		viewModel.ToggleOutputCommand.CanExecuteChanged+=(_,_)=>changes++;
		Assert.False(viewModel.ToggleOutputCommand.CanExecute(null));

		await viewModel.ConnectCommand.ExecuteAsync(null);

		Assert.True(viewModel.ToggleOutputCommand.CanExecute(null));
		Assert.True(changes>0);
		await viewModel.CloseAsync(CancellationToken.None);
	}

	[Fact]
	public async Task OnlineButton_DisconnectsBothSupplies()
	{
		FakeSessionFactory factory=new();
		DualSupplyViewModel viewModel=new(
			new PortLeaseRegistry(),
			factory,
			["COM5","COM6"]);
		await viewModel.ConnectCommand.ExecuteAsync(null);
		FakePowerSupplySession[] sessions=factory.Sessions.ToArray();

		await viewModel.ConnectCommand.ExecuteAsync(null);

		Assert.False(viewModel.IsConnected);
		Assert.All(sessions,session=>Assert.Contains(false,session.Outputs));
		Assert.All(sessions,session=>Assert.True(session.DisposeRequested));
	}

	[Fact]
	public void PortLists_PreventIdenticalSelections()
	{
		DualSupplyViewModel viewModel=new(
			new PortLeaseRegistry(),
			new FakeSessionFactory(),
			["COM5","COM6"])
		{
			FirstPortName="COM5",
			SecondPortName=" com5 "
		};

		Assert.False(viewModel.IsConnected);
		Assert.False(string.Equals(
			viewModel.SelectedFirstPort,
			viewModel.SelectedSecondPort,
			StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task Connect_PartialFailureIdentifiesFailingPortAndCleansUp()
	{
		PortLeaseRegistry leases=new();
		FakeSessionFactory factory=new(){FailPort="COM8"};
		DualSupplyViewModel viewModel=new(
			leases,
			factory,
			["COM7","COM8"],
			"COM7",
			"COM8");

		await viewModel.ConnectCommand.ExecuteAsync(null);

		Assert.False(viewModel.IsConnected);
		Assert.Contains("COM8",viewModel.ErrorMessage);
		Assert.True(leases.TryAcquire("COM7",out PortLease? first));
		Assert.True(leases.TryAcquire("COM8",out PortLease? second));
		first.Dispose();
		second.Dispose();
	}

	[Fact]
	public void RapidVoltageChanges_AreAcceptedSynchronously()
	{
		(DualSupplyViewModel viewModel,FakePowerSupplySession first,FakePowerSupplySession second)=
			CreateViewModel();

		for(int value=1200;value<=1250;value++)
		{
			viewModel.SetVoltageFromHundredths(value);
		}

		Assert.Equal("12,50",viewModel.VoltageText);
		Assert.Equal(625,first.RequestedVoltage?.Hundredths);
		Assert.Equal(625,second.RequestedVoltage?.Hundredths);
	}

	[Fact]
	public async Task ToggleOutput_UsesCommonControllerOperation()
	{
		(DualSupplyViewModel viewModel,FakePowerSupplySession first,FakePowerSupplySession second)=
			CreateViewModel();

		await viewModel.ToggleOutputCommand.ExecuteAsync(null);
		await viewModel.ToggleOutputCommand.ExecuteAsync(null);

		Assert.Equal([true,false],first.Outputs);
		Assert.Equal([true,false],second.Outputs);
		Assert.False(viewModel.IsOutputOn);
	}

	[Fact]
	public async Task ChangeMode_WhileOn_SendsOffAndLeavesOutputOff()
	{
		(DualSupplyViewModel viewModel,FakePowerSupplySession first,FakePowerSupplySession second)=
			CreateViewModel();
		await viewModel.ToggleOutputCommand.ExecuteAsync(null);

		await viewModel.ChangeModeCommand.ExecuteAsync(DualMode.Parallel);

		Assert.Equal(DualMode.Parallel,viewModel.Mode);
		Assert.False(first.Outputs[^1]);
		Assert.False(second.Outputs[^1]);
		Assert.False(viewModel.IsOutputOn);
		Assert.Equal(1200,first.RequestedVoltage?.Hundredths);
		Assert.Equal(1200,second.RequestedVoltage?.Hundredths);
	}

	[Fact]
	public async Task ChangeMode_OffFailure_KeepsPreviousMode()
	{
		(DualSupplyViewModel viewModel,FakePowerSupplySession first,_)=
			CreateViewModel();
		await viewModel.ToggleOutputCommand.ExecuteAsync(null);
		first.OutputFailure=new IOException("COM5");

		await viewModel.ChangeModeCommand.ExecuteAsync(DualMode.Parallel);

		Assert.Equal(DualMode.Series,viewModel.Mode);
		Assert.Contains("COM5",viewModel.ErrorMessage);
	}

	[Fact]
	public void Measurements_ShowLogicalAndSeparateDeviceValues()
	{
		(DualSupplyViewModel viewModel,FakePowerSupplySession first,FakePowerSupplySession second)=
			CreateViewModel();
		viewModel.Mode=DualMode.Series;

		first.PublishMeasurement(new MeasurementSample(TimeSpan.Zero,1200,400));
		second.PublishMeasurement(new MeasurementSample(TimeSpan.Zero,1100,350));

		Assert.Equal("23,00 V",viewModel.MeasuredVoltageText);
		Assert.Equal("0,400 A",viewModel.MeasuredCurrentText);
		Assert.Equal("12,00 V / 0,400 A",viewModel.FirstMeasurementText);
		Assert.Equal("11,00 V / 0,350 A",viewModel.SecondMeasurementText);
		Assert.False(viewModel.IsResistanceVisible);
	}

	[Fact]
	public void SymmetricMeasurements_ShowPositivePortOneAndNegativePortTwo()
	{
		(DualSupplyViewModel viewModel,FakePowerSupplySession first,FakePowerSupplySession second)=
			CreateViewModel();
		viewModel.Mode=DualMode.Symmetric;
		using ChartViewModel chart=viewModel.CreateChartViewModel(new FakeFileDialogService());

		first.PublishMeasurement(new MeasurementSample(TimeSpan.Zero,1200,400));
		second.PublishMeasurement(new MeasurementSample(TimeSpan.Zero,1100,350));

		Assert.True(viewModel.IsSymmetric);
		Assert.Equal("Port 1 (+)",viewModel.FirstSupplyLabel);
		Assert.Equal("Port 2 (-)",viewModel.SecondSupplyLabel);
		Assert.Equal("+12,00 V",viewModel.FirstSideVoltageText);
		Assert.Equal("+0,400 A",viewModel.FirstSideCurrentText);
		Assert.Equal("-11,00 V",viewModel.SecondSideVoltageText);
		Assert.Equal("-0,350 A",viewModel.SecondSideCurrentText);
		ChartPoint point=Assert.Single(chart.Points);
		Assert.True(point.IsSymmetric);
		Assert.Equal(12,point.FirstVoltageVolts);
		Assert.Equal(-11,point.SecondVoltageVolts);
		Assert.Equal(0.4,point.FirstCurrentAmperes);
		Assert.Equal(-0.35,point.SecondCurrentAmperes);
	}

	[Fact]
	public void Measurements_AtCurrentLimitShowTotalShortCircuitResistance()
	{
		(DualSupplyViewModel viewModel,FakePowerSupplySession first,FakePowerSupplySession second)=
			CreateViewModel();
		viewModel.Mode=DualMode.Series;

		first.PublishMeasurement(new MeasurementSample(TimeSpan.Zero,5,1000));
		second.PublishMeasurement(new MeasurementSample(TimeSpan.Zero,5,1000));

		Assert.True(viewModel.IsResistanceVisible);
		Assert.Equal("100 mΩ",viewModel.ResistanceText);
	}

	[Fact]
	public async Task CommunicationFailure_ClosesBothDualSessions()
	{
		(DualSupplyViewModel viewModel,FakePowerSupplySession first,FakePowerSupplySession second)=
			CreateViewModel();

		first.PublishError(new DeviceCommunicationException("utrata COM5"));
		await WaitUntilAsync(()=>!viewModel.IsConnected);

		Assert.True(first.DisposeRequested);
		Assert.True(second.DisposeRequested);
		Assert.Contains("utrata COM5",viewModel.ErrorMessage);
	}

	private static async Task WaitUntilAsync(Func<bool> condition)
	{
		using CancellationTokenSource timeout=new(TimeSpan.FromSeconds(2));
		while(!condition())
		{
			await Task.Delay(10,timeout.Token);
		}
	}

	private static (
		DualSupplyViewModel ViewModel,
		FakePowerSupplySession First,
		FakePowerSupplySession Second) CreateViewModel()
	{
		FakePowerSupplySession first=new();
		FakePowerSupplySession second=new();
		DualPowerSupplyController controller=
			new(first,second,DualMode.Series);
		return (new DualSupplyViewModel(controller),first,second);
	}

	private sealed class FakeSessionFactory : ISingleSessionFactory
	{
		public string? FailPort { get; init; }
		public List<FakePowerSupplySession> Sessions { get; }=[];

		public ValueTask<IPowerSupplySession> CreateAsync(
			string portName,
			CancellationToken cancellationToken)
		{
			if(string.Equals(portName,FailPort,StringComparison.OrdinalIgnoreCase))
			{
				throw new IOException(portName);
			}
			FakePowerSupplySession session=new();
			Sessions.Add(session);
			return ValueTask.FromResult<IPowerSupplySession>(session);
		}
	}
}
