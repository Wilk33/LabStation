using Ka3005P.App.Infrastructure;
using Ka3005P.App.Services;
using Ka3005P.Core.Configuration;

namespace Ka3005P.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject,IAsyncDisposable
{
	private readonly ISupplyModeFactory modeFactory;
	private readonly ISerialPortMonitor portMonitor;
	private readonly ISettingsStore settingsStore;
	private readonly SemaphoreSlim lifecycle=new(1,1);
	private readonly SynchronizationContext? uiContext=SynchronizationContext.Current;
	private AppSettings settings=new();
	private ISupplyModeViewModel currentMode=null!;
	private ApplicationMode selectedMode=ApplicationMode.Single;
	private string? errorMessage;
	private bool initialized;
	private bool disposed;

	public MainWindowViewModel(
		ISupplyModeFactory modeFactory,
		ISerialPortMonitor portMonitor,
		ISettingsStore settingsStore)
	{
		this.modeFactory=modeFactory;
		this.portMonitor=portMonitor;
		this.settingsStore=settingsStore;
		SelectSingleCommand=new AsyncRelayCommand(
			_=>SwitchModeAsync(ApplicationMode.Single),
			_=>initialized && SelectedMode != ApplicationMode.Single,
			exception=>ErrorMessage=exception.Message);
		SelectDualCommand=new AsyncRelayCommand(
			_=>SwitchModeAsync(ApplicationMode.Dual),
			_=>initialized && SelectedMode != ApplicationMode.Dual,
			exception=>ErrorMessage=exception.Message);
	}

	public ISupplyModeViewModel CurrentMode
	{
		get => currentMode;
		private set => SetProperty(ref currentMode,value);
	}

	public ApplicationMode SelectedMode
	{
		get => selectedMode;
		private set
		{
			if(SetProperty(ref selectedMode,value))
			{
				OnPropertyChanged(nameof(IsSingleSelected));
				OnPropertyChanged(nameof(IsDualSelected));
				OnPropertyChanged(nameof(WindowWidth));
				OnPropertyChanged(nameof(WindowHeight));
				SelectSingleCommand.RaiseCanExecuteChanged();
				SelectDualCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public bool IsSingleSelected => SelectedMode == ApplicationMode.Single;
	public bool IsDualSelected => SelectedMode == ApplicationMode.Dual;
	public double WindowWidth => IsSingleSelected ? 300 : 380;
	public double WindowHeight => IsSingleSelected ? 360 : 460;
	public AsyncRelayCommand SelectSingleCommand { get; }
	public AsyncRelayCommand SelectDualCommand { get; }

	public string? ErrorMessage
	{
		get => errorMessage;
		private set => SetProperty(ref errorMessage,value);
	}

	public async Task InitializeAsync(CancellationToken cancellationToken)
	{
		if(initialized)
		{
			return;
		}
		settings=await settingsStore.LoadAsync(cancellationToken);
		portMonitor.PortsChanged+=OnPortsChanged;
		await portMonitor.RefreshOnceAsync(cancellationToken);
		CurrentMode=modeFactory.Create(
			ApplicationMode.Single,
			portMonitor.Ports,
			settings);
		SelectedMode=ApplicationMode.Single;
		initialized=true;
		SelectDualCommand.RaiseCanExecuteChanged();
		portMonitor.Start();
	}

	public async ValueTask DisposeAsync()
	{
		if(disposed)
		{
			return;
		}
		await lifecycle.WaitAsync();
		try
		{
			if(disposed)
			{
				return;
			}
			disposed=true;
			portMonitor.PortsChanged-=OnPortsChanged;
			if(initialized)
			{
				RememberPorts(CurrentMode);
				await CurrentMode.CloseAsync(CancellationToken.None);
				await settingsStore.SaveAsync(settings,CancellationToken.None);
			}
			await portMonitor.DisposeAsync();
		}
		finally
		{
			lifecycle.Release();
		}
	}

	private async Task SwitchModeAsync(ApplicationMode target)
	{
		if(!initialized || target == SelectedMode)
		{
			return;
		}
		await lifecycle.WaitAsync();
		try
		{
			if(target == SelectedMode)
			{
				return;
			}
			RememberPorts(CurrentMode);
			await CurrentMode.CloseAsync(CancellationToken.None);
			await settingsStore.SaveAsync(settings,CancellationToken.None);
			CurrentMode=modeFactory.Create(target,portMonitor.Ports,settings);
			SelectedMode=target;
			ErrorMessage=null;
		}
		finally
		{
			lifecycle.Release();
		}
	}

	private void RememberPorts(ISupplyModeViewModel mode)
	{
		settings=mode.ApplicationMode switch
		{
			ApplicationMode.Single=>settings with
			{
				SinglePort=mode.PrimaryPort
			},
			ApplicationMode.Dual=>settings with
			{
				DualFirstPort=mode.PrimaryPort,
				DualSecondPort=mode.SecondaryPort
			},
			_=>settings
		};
	}

	private void OnPortsChanged(object? sender,IReadOnlyList<string> ports)
	{
		Dispatch(()=>CurrentMode?.UpdateAvailablePorts(ports));
	}

	private void Dispatch(Action action)
	{
		if(uiContext is null || SynchronizationContext.Current == uiContext)
		{
			action();
		}
		else
		{
			uiContext.Post(_=>action(),null);
		}
	}
}
