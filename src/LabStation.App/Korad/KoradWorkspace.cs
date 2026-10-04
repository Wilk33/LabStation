using Ka3005P.App.Services;
using Ka3005P.App.ViewModels;
using Ka3005P.Core.Configuration;
using Ka3005P.Core.Measurements;

namespace LabStation.App.Korad;

public interface IKoradWorkspaceModeFactory
{
	ISupplyModeViewModel CreateSingle(
		IReadOnlyList<string> ports,
		string? preferredPort);

	ISupplyModeViewModel CreateDual(
		IReadOnlyList<string> ports,
		string? firstPort,
		string? secondPort);
}

public sealed class KoradWorkspaceModeFactory : IKoradWorkspaceModeFactory
{
	private readonly PortLeaseRegistry leases;
	private readonly ISingleSessionFactory sessionFactory;

	public KoradWorkspaceModeFactory(
		PortLeaseRegistry leases,
		ISingleSessionFactory sessionFactory)
	{
		this.leases=leases ?? throw new ArgumentNullException(nameof(leases));
		this.sessionFactory=sessionFactory ??
			throw new ArgumentNullException(nameof(sessionFactory));
	}

	public ISupplyModeViewModel CreateSingle(
		IReadOnlyList<string> ports,
		string? preferredPort)=>new SingleSupplyViewModel(
			leases,
			sessionFactory,
			ports,
			preferredPort);

	public ISupplyModeViewModel CreateDual(
		IReadOnlyList<string> ports,
		string? firstPort,
		string? secondPort)=>new DualSupplyViewModel(
			leases,
			sessionFactory,
			ports,
			firstPort,
			secondPort);
}

public sealed class KoradWorkspace : IAsyncDisposable
{
	private sealed class NoFileDialogService : IFileDialogService
	{
		public ValueTask<string?> ChooseSavePathAsync(
			string suggestedFileName,
			CancellationToken cancellationToken)=>
			ValueTask.FromResult<string?>(null);
	}

	private readonly IKoradWorkspaceModeFactory modeFactory;
	private readonly ISerialPortMonitor portMonitor;
	private readonly IKoradWorkspaceSettingsStore settingsStore;
	private readonly IFileDialogService chartFileDialog;
	private readonly SemaphoreSlim lifecycle=new(1,1);
	private readonly List<ISupplyModeViewModel> modes=[];
	private readonly List<ChartViewModel> charts=[];
	private KoradWorkspaceSettings settings=KoradWorkspaceSettings.Default;
	private bool initialized;
	private bool switching;
	private bool disposed;

	public KoradWorkspace(
		IKoradWorkspaceModeFactory modeFactory,
		ISerialPortMonitor portMonitor,
		IKoradWorkspaceSettingsStore settingsStore,
		IFileDialogService? chartFileDialog=null)
	{
		this.modeFactory=modeFactory ??
			throw new ArgumentNullException(nameof(modeFactory));
		this.portMonitor=portMonitor ??
			throw new ArgumentNullException(nameof(portMonitor));
		this.settingsStore=settingsStore ??
			throw new ArgumentNullException(nameof(settingsStore));
		this.chartFileDialog=chartFileDialog ?? new NoFileDialogService();
	}

	public event EventHandler? Changed;

	public KoradConfiguration CurrentConfiguration { get;private set; }=
		KoradConfiguration.OneSingle;

	public IReadOnlyList<ISupplyModeViewModel> Modes=>modes;
	public IReadOnlyList<ChartViewModel> Charts=>charts;
	public bool CanChangeConfiguration=>
		initialized && !switching && modes.All(mode=>!mode.IsOutputOn);

	public async Task InitializeAsync(CancellationToken cancellationToken)
	{
		if(initialized)
		{
			return;
		}
		settings=await settingsStore.LoadAsync(cancellationToken);
		CurrentConfiguration=settings.Configuration;
		portMonitor.PortsChanged+=OnPortsChanged;
		await portMonitor.RefreshOnceAsync(cancellationToken);
		CreateModes(CurrentConfiguration);
		initialized=true;
		portMonitor.Start();
		Changed?.Invoke(this,EventArgs.Empty);
	}

	public async Task<bool> SetConfigurationAsync(
		KoradConfiguration configuration,
		CancellationToken cancellationToken)
	{
		if(!initialized || disposed || configuration == CurrentConfiguration)
		{
			return initialized && !disposed;
		}
		if(!CanChangeConfiguration)
		{
			return false;
		}
		await lifecycle.WaitAsync(cancellationToken);
		try
		{
			if(!CanChangeConfiguration)
			{
				return false;
			}
			switching=true;
			RememberPorts();
			await CloseModesAsync(cancellationToken);
			CurrentConfiguration=configuration;
			settings=settings with { Configuration=configuration };
			CreateModes(configuration);
			await settingsStore.SaveAsync(settings,cancellationToken);
			return true;
		}
		finally
		{
			switching=false;
			lifecycle.Release();
			Changed?.Invoke(this,EventArgs.Empty);
		}
	}

	public async ValueTask ExportAsync(
		MeasurementExportKind kind,
		string path,
		CancellationToken cancellationToken)
	{
		if(!initialized || modes.Count == 0)
		{
			return;
		}
		KoradExportPaths paths=KoradExportPaths.ForConfiguration(
			path,
			CurrentConfiguration);
		await modes[0].ExportAsync(kind,paths.PrimaryPath,cancellationToken);
		if(paths.SecondaryPath is not null && modes.Count>1)
		{
			await modes[1].ExportAsync(
				kind,
				paths.SecondaryPath,
				cancellationToken);
		}
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
				RememberPorts();
				await CloseModesAsync(CancellationToken.None);
				await settingsStore.SaveAsync(settings,CancellationToken.None);
			}
			await portMonitor.DisposeAsync();
		}
		finally
		{
			lifecycle.Release();
		}
	}

	private void CreateModes(KoradConfiguration configuration)
	{
		IReadOnlyList<string> ports=portMonitor.Ports;
		switch(configuration)
		{
			case KoradConfiguration.OneSingle:
				modes.Add(modeFactory.CreateSingle(
					ports,
					settings.FirstSinglePort));
				break;
			case KoradConfiguration.TwoSingle:
				modes.Add(modeFactory.CreateSingle(
					ports,
					settings.FirstSinglePort));
				modes.Add(modeFactory.CreateSingle(
					ports,
					settings.SecondSinglePort));
				break;
			case KoradConfiguration.Dual:
				modes.Add(modeFactory.CreateDual(
					ports,
					settings.DualFirstPort,
					settings.DualSecondPort));
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(configuration));
		}
		foreach(ISupplyModeViewModel mode in modes)
		{
			mode.OutputStateChanged+=OnOutputStateChanged;
			charts.Add(mode.CreateChartViewModel(chartFileDialog));
		}
	}

	private async ValueTask CloseModesAsync(
		CancellationToken cancellationToken)
	{
		foreach(ChartViewModel chart in charts)
		{
			chart.Dispose();
		}
		charts.Clear();
		ISupplyModeViewModel[] current=[..modes];
		modes.Clear();
		foreach(ISupplyModeViewModel mode in current)
		{
			mode.OutputStateChanged-=OnOutputStateChanged;
			await mode.CloseAsync(cancellationToken);
		}
	}

	private void RememberPorts()
	{
		settings=CurrentConfiguration switch
		{
			KoradConfiguration.OneSingle=>settings with
			{
				FirstSinglePort=modes.FirstOrDefault()?.PrimaryPort
			},
			KoradConfiguration.TwoSingle=>settings with
			{
				FirstSinglePort=modes.ElementAtOrDefault(0)?.PrimaryPort,
				SecondSinglePort=modes.ElementAtOrDefault(1)?.PrimaryPort
			},
			KoradConfiguration.Dual=>settings with
			{
				DualFirstPort=modes.FirstOrDefault()?.PrimaryPort,
				DualSecondPort=modes.FirstOrDefault()?.SecondaryPort
			},
			_=>settings
		};
	}

	private void OnPortsChanged(
		object? sender,
		IReadOnlyList<string> ports)
	{
		foreach(ISupplyModeViewModel mode in modes)
		{
			mode.UpdateAvailablePorts(ports);
		}
	}

	private void OnOutputStateChanged(object? sender,bool enabled)
	{
		Changed?.Invoke(this,EventArgs.Empty);
	}
}
