using Ka3005P.App.Services;
using Ka3005P.App.ViewModels;
using Ka3005P.Core.Configuration;
using Ka3005P.Core.Measurements;

namespace Ka3005P.Tests.ViewModels;

public sealed class MainWindowViewModelTests
{
	[Fact]
	public async Task Initialize_StartsInSingleModeWithFixedSingleSize()
	{
		FakeModeFactory factory=new();
		await using MainWindowViewModel viewModel=new(
			factory,
			new FakePortMonitor(["COM5","COM6"]),
			new MemorySettingsStore());

		await viewModel.InitializeAsync(CancellationToken.None);

		Assert.Equal(ApplicationMode.Single,viewModel.SelectedMode);
		Assert.True(viewModel.IsSingleSelected);
		Assert.False(viewModel.IsDualSelected);
		Assert.Equal(300,viewModel.WindowWidth);
		Assert.Equal(360,viewModel.WindowHeight);
	}

	[Fact]
	public async Task SelectDual_ClosesSingleAndCreatesFreshOfflineDual()
	{
		FakeModeFactory factory=new();
		await using MainWindowViewModel viewModel=new(
			factory,
			new FakePortMonitor(["COM5","COM6"]),
			new MemorySettingsStore());
		await viewModel.InitializeAsync(CancellationToken.None);
		FakeMode single=Assert.IsType<FakeMode>(viewModel.CurrentMode);

		await viewModel.SelectDualCommand.ExecuteAsync(null);

		Assert.True(single.Closed);
		Assert.Equal(ApplicationMode.Dual,viewModel.SelectedMode);
		Assert.False(viewModel.CurrentMode.IsConnected);
		Assert.Equal(380,viewModel.WindowWidth);
		Assert.Equal(460,viewModel.WindowHeight);
	}

	private sealed class FakeModeFactory : ISupplyModeFactory
	{
		public ISupplyModeViewModel Create(
			ApplicationMode mode,
			IReadOnlyList<string> ports,
			AppSettings settings)
		{
			return new FakeMode(mode);
		}
	}

	private sealed class FakeMode : ISupplyModeViewModel
	{
		public FakeMode(ApplicationMode mode)
		{
			ApplicationMode=mode;
		}

		public event EventHandler<bool>? OutputStateChanged
		{
			add { }
			remove { }
		}
		public event EventHandler<bool>? ConnectionStateChanged
		{
			add { }
			remove { }
		}
		public event EventHandler<ChartSample>? ChartSampleReceived
		{
			add { }
			remove { }
		}
		public ApplicationMode ApplicationMode { get; }
		public bool IsOutputOn => false;
		public bool IsConnected => false;
		public string? PrimaryPort => null;
		public string? SecondaryPort => null;
		public bool Closed { get; private set; }

		public void UpdateAvailablePorts(IReadOnlyList<string> ports)
		{
		}

		public ChartViewModel CreateChartViewModel(IFileDialogService fileDialog)
		{
			return new ChartViewModel(this);
		}

		public ValueTask SetOutputAsync(bool enabled,CancellationToken cancellationToken)
		{
			return ValueTask.CompletedTask;
		}

		public ValueTask ExportAsync(
			MeasurementExportKind kind,
			string path,
			CancellationToken cancellationToken)
		{
			return ValueTask.CompletedTask;
		}

		public ValueTask CloseAsync(CancellationToken cancellationToken)
		{
			Closed=true;
			return ValueTask.CompletedTask;
		}
	}

	private sealed class FakePortMonitor : ISerialPortMonitor
	{
		public FakePortMonitor(IReadOnlyList<string> ports)
		{
			Ports=ports;
		}

		public event SerialPortsChangedEventHandler? PortsChanged;
		public IReadOnlyList<string> Ports { get; }
		public Exception? LastError => null;
		public void Start()
		{
		}
		public ValueTask RefreshOnceAsync(CancellationToken cancellationToken)
		{
			PortsChanged?.Invoke(this,Ports);
			return ValueTask.CompletedTask;
		}
		public ValueTask DisposeAsync()
		{
			return ValueTask.CompletedTask;
		}
	}

	private sealed class MemorySettingsStore : ISettingsStore
	{
		public AppSettings Settings { get; private set; }=new();

		public ValueTask<AppSettings> LoadAsync(CancellationToken cancellationToken)
		{
			return ValueTask.FromResult(Settings);
		}

		public ValueTask SaveAsync(
			AppSettings settings,
			CancellationToken cancellationToken)
		{
			Settings=settings;
			return ValueTask.CompletedTask;
		}
	}
}
