using Ka3005P.App.Services;
using Ka3005P.App.ViewModels;
using Ka3005P.Core.Configuration;
using Ka3005P.Core.Device;
using Ka3005P.Core.Measurements;
using Ka3005P.Core.Sessions;
using Ka3005P.Tests.Fakes;

namespace Ka3005P.Tests.ViewModels;

public sealed class SingleSupplyViewModelTests
{
	[Fact]
	public async Task StatusText_FollowsOfflineOffAndOn()
	{
		SingleSupplyViewModel offline=new(
			new PortLeaseRegistry(),
			new FakeSingleSessionFactory(),
			["COM5"]);
		Assert.Equal("Stan zasilacza: OFFLINE",offline.StatusText);

		FakePowerSupplySession session=new();
		SingleSupplyViewModel online=new(session);
		Assert.Equal("Stan zasilacza: OFF",online.StatusText);
		await online.ToggleOutputCommand.ExecuteAsync(null);
		Assert.Equal("Stan zasilacza: ON",online.StatusText);
	}

	[Fact]
	public void AvailablePorts_RestorePreferredOrChooseFirst()
	{
		SingleSupplyViewModel preferred=new(
			new PortLeaseRegistry(),
			new FakeSingleSessionFactory(),
			["COM2","COM7"],
			"COM7");
		SingleSupplyViewModel fallback=new(
			new PortLeaseRegistry(),
			new FakeSingleSessionFactory(),
			["COM2","COM7"],
			"COM9");

		Assert.Equal("COM7",preferred.SelectedPort);
		Assert.Equal("COM2",fallback.SelectedPort);
		Assert.True(preferred.ConnectCommand.CanExecute(null));
	}

	[Fact]
	public void NoAvailablePort_DisablesConnect()
	{
		SingleSupplyViewModel viewModel=new(
			new PortLeaseRegistry(),
			new FakeSingleSessionFactory(),
			[]);

		Assert.Null(viewModel.SelectedPort);
		Assert.False(viewModel.ConnectCommand.CanExecute(null));
	}

	[Fact]
	public void IncrementVoltage_UpdatesUiAndQueuesRequestSynchronously()
	{
		FakePowerSupplySession session=new();
		SingleSupplyViewModel viewModel=new(session){VoltageText="12,00"};

		viewModel.IncrementVoltageCommand.Execute(null);

		Assert.Equal("12,01",viewModel.VoltageText);
		Assert.Equal(1201,session.RequestedVoltage?.Hundredths);
		Assert.False(viewModel.HasValidationError);
	}

	[Theory]
	[InlineData("12,34",1234)]
	[InlineData("12.34",1234)]
	public void CommitVoltage_AcceptsCommaAndDot(string text,int expected)
	{
		FakePowerSupplySession session=new();
		SingleSupplyViewModel viewModel=new(session){VoltageText=text};

		viewModel.CommitVoltageCommand.Execute(null);

		Assert.Equal(expected,session.RequestedVoltage?.Hundredths);
		Assert.Equal("12,34",viewModel.VoltageText);
	}

	[Theory]
	[InlineData("")]
	[InlineData("-1")]
	[InlineData("31,01")]
	[InlineData("abc")]
	public void CommitVoltage_InvalidValueDoesNotQueueRequest(string text)
	{
		FakePowerSupplySession session=new();
		SingleSupplyViewModel viewModel=new(session){VoltageText=text};

		viewModel.CommitVoltageCommand.Execute(null);

		Assert.Null(session.RequestedVoltage);
		Assert.True(viewModel.HasValidationError);
	}

	[Theory]
	[InlineData("5,100",5100,false)]
	[InlineData("5.100",5100,false)]
	[InlineData("5,101",0,true)]
	[InlineData("",0,true)]
	public void CommitCurrent_ValidatesDeviceRange(
		string text,
		int expected,
		bool invalid)
	{
		FakePowerSupplySession session=new();
		SingleSupplyViewModel viewModel=new(session){CurrentText=text};

		viewModel.CommitCurrentCommand.Execute(null);

		Assert.Equal(invalid,viewModel.HasValidationError);
		if(invalid)
		{
			Assert.Null(session.RequestedCurrent);
		}
		else
		{
			Assert.Equal(expected,session.RequestedCurrent?.Thousandths);
		}
	}

	[Fact]
	public void Measurement_UpdatesOutputWithoutChangingSetpoints()
	{
		FakePowerSupplySession session=new();
		SingleSupplyViewModel viewModel=new(session){VoltageText="12,00"};
		viewModel.CommitVoltageCommand.Execute(null);

		session.PublishMeasurement(
			new MeasurementSample(TimeSpan.FromSeconds(1),0,0));

		Assert.Equal("12,00",viewModel.VoltageText);
		Assert.Equal("0,00 V",viewModel.MeasuredVoltageText);
		Assert.Equal("0,000 A",viewModel.MeasuredCurrentText);
		Assert.False(viewModel.IsResistanceVisible);
	}

	[Fact]
	public void Measurement_WithCurrentShowsResistance()
	{
		FakePowerSupplySession session=new();
		SingleSupplyViewModel viewModel=new(session);

		session.PublishMeasurement(
			new MeasurementSample(TimeSpan.Zero,1200,1000));

		Assert.True(viewModel.IsResistanceVisible);
		Assert.Equal("12 Ω",viewModel.ResistanceText);
	}

	[Fact]
	public void Measurement_BelowCurrentLimitHidesShortCircuitResistance()
	{
		FakePowerSupplySession session=new();
		SingleSupplyViewModel viewModel=new(session);

		session.PublishMeasurement(
			new MeasurementSample(TimeSpan.Zero,1200,500));

		Assert.False(viewModel.IsResistanceVisible);
		Assert.Null(viewModel.ResistanceText);
	}

	[Fact]
	public async Task Connect_SendsDisplayedSetpoints()
	{
		FakeSingleSessionFactory factory=new();
		SingleSupplyViewModel viewModel=new(new PortLeaseRegistry(),factory);

		await viewModel.ConnectCommand.ExecuteAsync(null);

		FakePowerSupplySession session=Assert.IsType<FakePowerSupplySession>(
			factory.LastSession);
		Assert.Equal(1200,session.RequestedVoltage?.Hundredths);
		Assert.Equal(1000,session.RequestedCurrent?.Thousandths);
		await viewModel.CloseAsync(CancellationToken.None);
	}

	[Fact]
	public async Task Connect_EnablesOutputCommand()
	{
		SingleSupplyViewModel viewModel=new(
			new PortLeaseRegistry(),
			new FakeSingleSessionFactory());
		int changes=0;
		viewModel.ToggleOutputCommand.CanExecuteChanged+=(_,_)=>changes++;
		Assert.False(viewModel.ToggleOutputCommand.CanExecute(null));

		await viewModel.ConnectCommand.ExecuteAsync(null);

		Assert.True(viewModel.ToggleOutputCommand.CanExecute(null));
		Assert.True(changes>0);
		await viewModel.CloseAsync(CancellationToken.None);
	}

	[Fact]
	public async Task OnlineButton_DisconnectsAndAllowsReconnect()
	{
		FakeSingleSessionFactory factory=new();
		SingleSupplyViewModel viewModel=new(
			new PortLeaseRegistry(),
			factory,
			["COM5"]);
		await viewModel.ConnectCommand.ExecuteAsync(null);
		FakePowerSupplySession first=Assert.IsType<FakePowerSupplySession>(
			factory.LastSession);

		await viewModel.ConnectCommand.ExecuteAsync(null);

		Assert.False(viewModel.IsConnected);
		Assert.Contains(false,first.Outputs);
		Assert.True(first.StopRequested);
		Assert.True(viewModel.ConnectCommand.CanExecute(null));
		await viewModel.ConnectCommand.ExecuteAsync(null);
		Assert.True(viewModel.IsConnected);
		await viewModel.CloseAsync(CancellationToken.None);
	}

	[Fact]
	public async Task MainAndChartOutputCommands_AreSerialized()
	{
		FakePowerSupplySession session=new();
		SingleSupplyViewModel main=new(session);
		using ChartViewModel chart=main.CreateChartViewModel(
			new FakeFileDialogService());
		session.BlockNextOutput();

		Task mainOperation=main.ToggleOutputCommand.ExecuteAsync(null);
		await session.WaitUntilOutputStartsAsync();
		Task chartOperation=chart.ToggleOutputCommand.ExecuteAsync(null);
		await Task.Delay(50);

		Assert.False(chartOperation.IsCompleted);
		session.ReleaseOutput();
		await Task.WhenAll(mainOperation,chartOperation)
			.WaitAsync(TimeSpan.FromSeconds(2));
	}

	[Fact]
	public async Task Connect_NotifiesOffLampState()
	{
		SingleSupplyViewModel viewModel=new(
			new PortLeaseRegistry(),
			new FakeSingleSessionFactory());
		List<string?> changed=[];
		viewModel.PropertyChanged+=(_,args)=>changed.Add(args.PropertyName);

		await viewModel.ConnectCommand.ExecuteAsync(null);

		Assert.True(viewModel.IsOff);
		Assert.Contains(nameof(viewModel.IsOff),changed);
		await viewModel.CloseAsync(CancellationToken.None);
	}

	[Fact]
	public async Task Connect_RejectsPortAlreadyLeasedByAnotherWindow()
	{
		PortLeaseRegistry leases=new();
		FakeSingleSessionFactory factory=new();
		SingleSupplyViewModel first=new(leases,factory){PortName="COM5"};
		SingleSupplyViewModel second=new(leases,factory){PortName="com5"};
		await first.ConnectCommand.ExecuteAsync(null);

		await second.ConnectCommand.ExecuteAsync(null);

		Assert.True(first.IsConnected);
		Assert.False(second.IsConnected);
		Assert.Contains("używany",second.ErrorMessage,StringComparison.OrdinalIgnoreCase);
		await first.CloseAsync(CancellationToken.None);
		await second.CloseAsync(CancellationToken.None);
	}

	[Fact]
	public async Task CloseAsync_SendsOffStopsSessionAndReleasesPort()
	{
		PortLeaseRegistry leases=new();
		FakeSingleSessionFactory factory=new();
		SingleSupplyViewModel viewModel=new(
			leases,
			factory,
			["COM7"],
			"COM7");
		await viewModel.ConnectCommand.ExecuteAsync(null);
		FakePowerSupplySession session=Assert.IsType<FakePowerSupplySession>(factory.LastSession);

		await viewModel.CloseAsync(CancellationToken.None);

		Assert.Contains(false,session.Outputs);
		Assert.True(session.StopRequested);
		Assert.True(leases.TryAcquire("COM7",out PortLease? lease));
		lease.Dispose();
	}

	[Fact]
	public async Task CommunicationFailure_TransitionsOfflineAndDisposesSession()
	{
		FakePowerSupplySession session=new();
		SingleSupplyViewModel viewModel=new(session);

		session.PublishError(new DeviceCommunicationException("utrata COM5"));
		await WaitUntilAsync(()=>!viewModel.IsConnected);

		Assert.True(session.StopRequested);
		Assert.True(session.DisposeRequested);
		Assert.Contains("utrata COM5",viewModel.ErrorMessage);
	}

	[Fact]
	public async Task CommunicationFailure_AllowsReconnect()
	{
		FakeSingleSessionFactory factory=new();
		SingleSupplyViewModel viewModel=new(
			new PortLeaseRegistry(),
			factory,
			["COM5"]);
		await viewModel.ConnectCommand.ExecuteAsync(null);
		FakePowerSupplySession first=Assert.IsType<FakePowerSupplySession>(
			factory.LastSession);

		first.PublishError(new DeviceCommunicationException("utrata COM5"));
		await WaitUntilAsync(()=>!viewModel.IsConnected);
		await viewModel.ConnectCommand.ExecuteAsync(null);

		Assert.True(viewModel.IsConnected);
		Assert.NotSame(first,factory.LastSession);
		await viewModel.CloseAsync(CancellationToken.None);
	}

	private static async Task WaitUntilAsync(Func<bool> condition)
	{
		using CancellationTokenSource timeout=new(TimeSpan.FromSeconds(2));
		while(!condition())
		{
			await Task.Delay(10,timeout.Token);
		}
	}

	private sealed class FakeSingleSessionFactory : ISingleSessionFactory
	{
		public IPowerSupplySession? LastSession { get; private set; }

		public ValueTask<IPowerSupplySession> CreateAsync(
			string portName,
			CancellationToken cancellationToken)
		{
			LastSession=new FakePowerSupplySession();
			return ValueTask.FromResult(LastSession);
		}
	}
}
