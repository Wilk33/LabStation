using System.IO;
using System.Windows;
using System.Windows.Controls;
using Ka3005P.App.Services;
using Ka3005P.App.ViewModels;
using Ka3005P.App.Views;
using Ka3005P.Core.Configuration;
using Ka3005P.Core.Measurements;

namespace LabStation.App.Korad;

public partial class KoradPanel : UserControl,IAsyncDisposable
{
	private readonly KoradWorkspace workspace;
	private bool initialized;
	private bool disposed;

	public KoradPanel()
		:this(CreateWorkspace())
	{
	}

	internal KoradPanel(KoradWorkspace workspace)
	{
		this.workspace=workspace ?? throw new ArgumentNullException(nameof(workspace));
		InitializeComponent();
		workspace.Changed+=WorkspaceChanged;
		Loaded+=OnLoaded;
		Unloaded+=OnUnloaded;
	}

	public event EventHandler? CommandStateChanged;

	public KoradConfiguration CurrentConfiguration=>
		workspace.CurrentConfiguration;

	public bool CanChangeConfiguration=>workspace.CanChangeConfiguration;

	public async Task<bool> SetConfigurationAsync(
		KoradConfiguration configuration)
	{
		bool changed=await workspace.SetConfigurationAsync(
			configuration,
			CancellationToken.None);
		if(changed)
		{
			BuildLayout();
		}
		return changed;
	}

	public async Task ExportAsync(MeasurementExportKind kind)
	{
		FileDialogService dialog=new();
		string name=kind switch
		{
			MeasurementExportKind.Voltage=>"napiecie.csv",
			MeasurementExportKind.Current=>"prad.csv",
			MeasurementExportKind.VoltageAndCurrent=>"napiecie-i-prad.csv",
			_=>throw new ArgumentOutOfRangeException(nameof(kind))
		};
		string? path=await dialog.ChooseSavePathAsync(
			name,
			CancellationToken.None);
		if(path is not null)
		{
			await workspace.ExportAsync(kind,path,CancellationToken.None);
		}
	}

	public async ValueTask DisposeAsync()
	{
		if(disposed)
		{
			return;
		}
		disposed=true;
		Loaded-=OnLoaded;
		Unloaded-=OnUnloaded;
		workspace.Changed-=WorkspaceChanged;
		await workspace.DisposeAsync();
	}

	private static KoradWorkspace CreateWorkspace()
	{
		PortLeaseRegistry leases=new();
		ISingleSessionFactory sessionFactory=
			new SerialSingleSessionFactory(TimeProvider.System);
		ISerialPortMonitor monitor=new SerialPortMonitor(
			new SystemSerialPortCatalog(),
			TimeSpan.FromSeconds(1));
		string? settingsOverride=Environment.GetEnvironmentVariable(
			"LABSTATION_KORAD_SETTINGS_PATH");
		string settingsPath=string.IsNullOrWhiteSpace(settingsOverride)
			? Path.Combine(
				Environment.GetFolderPath(
					Environment.SpecialFolder.LocalApplicationData),
				AppInformation.DataDirectoryName,
				"korad.json")
			: Path.GetFullPath(settingsOverride);
		return new KoradWorkspace(
			new KoradWorkspaceModeFactory(leases,sessionFactory),
			monitor,
			new JsonKoradWorkspaceSettingsStore(settingsPath),
			new FileDialogService());
	}

	private async void OnLoaded(object sender,RoutedEventArgs eventArgs)
	{
		if(initialized || disposed)
		{
			return;
		}
		initialized=true;
		await workspace.InitializeAsync(CancellationToken.None);
		BuildLayout();
	}

	private async void OnUnloaded(object sender,RoutedEventArgs eventArgs)
	{
		await DisposeAsync();
	}

	private void WorkspaceChanged(object? sender,EventArgs eventArgs)
	{
		CommandStateChanged?.Invoke(this,EventArgs.Empty);
	}

	private void BuildLayout()
	{
		ControlHost.Children.Clear();
		ControlHost.ColumnDefinitions.Clear();
		ChartHost.Children.Clear();
		ChartHost.ColumnDefinitions.Clear();
		IReadOnlyList<ISupplyModeViewModel> modes=workspace.Modes;
		IReadOnlyList<ChartViewModel> charts=workspace.Charts;
		if(modes.Count == 0)
		{
			return;
		}
		if(workspace.CurrentConfiguration == KoradConfiguration.TwoSingle)
		{
			ControlColumn.Width=new GridLength(600);
			AddColumns(ControlHost,2);
			AddColumns(ChartHost,2);
			for(int index=0;index<2;index++)
			{
				SingleSupplyView control=new()
				{
					DataContext=modes[index]
				};
				Grid.SetColumn(control,index);
				ControlHost.Children.Add(control);
				EmbeddedChartView chart=new()
				{
					DataContext=charts[index]
				};
				Grid.SetColumn(chart,index);
				ChartHost.Children.Add(chart);
			}
		}
		else
		{
			ControlColumn.Width=new GridLength(
				workspace.CurrentConfiguration == KoradConfiguration.Dual
					? 390
					: 300);
			FrameworkElement control=
				workspace.CurrentConfiguration == KoradConfiguration.Dual
					? new CompactDualSupplyView()
					: new SingleSupplyView();
			control.DataContext=modes[0];
			ControlHost.Children.Add(control);
			ChartHost.Children.Add(new EmbeddedChartView
			{
				DataContext=charts[0]
			});
		}
		foreach(ChartViewModel chart in charts)
		{
			chart.ShowCurrent=true;
			chart.ShowVoltage=true;
		}
		CommandStateChanged?.Invoke(this,EventArgs.Empty);
	}

	private static void AddColumns(Grid grid,int count)
	{
		for(int index=0;index<count;index++)
		{
			grid.ColumnDefinitions.Add(new ColumnDefinition());
		}
	}
}
