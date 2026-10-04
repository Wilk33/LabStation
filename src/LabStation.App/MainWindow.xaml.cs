using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Ka3005P.Core.Measurements;
using LabStation.App.Korad;
using LabStation.UI;

namespace LabStation.App;

public partial class MainWindow : Window
{
	private bool closeCompleted;

	public MainWindow()
	{
		InitializeComponent();
		Title=AppInformation.DisplayName;
		SystemTheme.ApplyTo(this);
		KoradInstrumentPanel.CommandStateChanged+=PanelCommandStateChanged;
		OscilloscopeInstrumentPanel.CommandStateChanged+=PanelCommandStateChanged;
		GeneratorInstrumentPanel.CommandStateChanged+=PanelCommandStateChanged;
		MultimeterInstrumentPanel.CommandStateChanged+=PanelCommandStateChanged;
		LoadInstrumentPanel.CommandStateChanged+=PanelCommandStateChanged;
		UpdateMenuState();
	}

	public PanelLayout LayoutDefinition=>PanelLayout.Default;

	private void PanelCommandStateChanged(object? sender,EventArgs eventArgs)=>
		UpdateMenuState();

	private void UpdateMenuState()
	{
		KoradConfiguration configuration=
			KoradInstrumentPanel.CurrentConfiguration;
		KoradOneSingleMenuItem.IsChecked=
			configuration == KoradConfiguration.OneSingle;
		KoradTwoSingleMenuItem.IsChecked=
			configuration == KoradConfiguration.TwoSingle;
		KoradDualMenuItem.IsChecked=
			configuration == KoradConfiguration.Dual;
		bool canChange=KoradInstrumentPanel.CanChangeConfiguration;
		KoradOneSingleMenuItem.IsEnabled=canChange ||
			configuration == KoradConfiguration.OneSingle;
		KoradTwoSingleMenuItem.IsEnabled=canChange ||
			configuration == KoradConfiguration.TwoSingle;
		KoradDualMenuItem.IsEnabled=canChange ||
			configuration == KoradConfiguration.Dual;

		ScopeScanMenuItem.IsEnabled=OscilloscopeInstrumentPanel.CanScanNetwork;
		ScopeAutoConnectMenuItem.IsChecked=
			OscilloscopeInstrumentPanel.AutoConnect;
		ScopeSaveChannel1MenuItem.IsEnabled=
			OscilloscopeInstrumentPanel.CanSaveChannel1;
		ScopeSaveChannel2MenuItem.IsEnabled=
			OscilloscopeInstrumentPanel.CanSaveChannel2;
		ScopeSaveBothMenuItem.IsEnabled=
			OscilloscopeInstrumentPanel.CanSaveBothChannels;

		GeneratorScanMenuItem.IsEnabled=
			GeneratorInstrumentPanel.CanScanNetwork;
		GeneratorAutoConnectMenuItem.IsChecked=
			GeneratorInstrumentPanel.AutoConnect;

		MultimeterScanMenuItem.IsEnabled=
			MultimeterInstrumentPanel.CanScanNetwork;
		MultimeterAutoConnectMenuItem.IsChecked=
			MultimeterInstrumentPanel.AutoConnect;
		MultimeterSaveMenuItem.IsEnabled=
			MultimeterInstrumentPanel.CanExport;

		LoadScanMenuItem.IsEnabled=LoadInstrumentPanel.CanScanNetwork;
		LoadAutoConnectMenuItem.IsChecked=LoadInstrumentPanel.AutoConnect;
	}

	private async void KoradOneSingleClick(
		object sender,
		RoutedEventArgs eventArgs)=>await SetKoradConfigurationAsync(
		KoradConfiguration.OneSingle);

	private async void KoradTwoSingleClick(
		object sender,
		RoutedEventArgs eventArgs)=>await SetKoradConfigurationAsync(
		KoradConfiguration.TwoSingle);

	private async void KoradDualClick(
		object sender,
		RoutedEventArgs eventArgs)=>await SetKoradConfigurationAsync(
		KoradConfiguration.Dual);

	private async Task SetKoradConfigurationAsync(
		KoradConfiguration configuration)
	{
		await KoradInstrumentPanel.SetConfigurationAsync(configuration);
		UpdateMenuState();
	}

	private async void KoradSaveVoltageClick(
		object sender,
		RoutedEventArgs eventArgs)=>await KoradInstrumentPanel.ExportAsync(
		MeasurementExportKind.Voltage);

	private async void KoradSaveCurrentClick(
		object sender,
		RoutedEventArgs eventArgs)=>await KoradInstrumentPanel.ExportAsync(
		MeasurementExportKind.Current);

	private async void KoradSaveBothClick(
		object sender,
		RoutedEventArgs eventArgs)=>await KoradInstrumentPanel.ExportAsync(
		MeasurementExportKind.VoltageAndCurrent);

	private async void ScopeScanClick(
		object sender,
		RoutedEventArgs eventArgs)=>
		await OscilloscopeInstrumentPanel.ScanNetworkAsync();

	private void ScopeAutoConnectClick(
		object sender,
		RoutedEventArgs eventArgs)=>
		OscilloscopeInstrumentPanel.AutoConnect=
			ScopeAutoConnectMenuItem.IsChecked == true;

	private async void ScopeSaveChannel1Click(
		object sender,
		RoutedEventArgs eventArgs)=>
		await OscilloscopeInstrumentPanel.SaveCsvAsync([1]);

	private async void ScopeSaveChannel2Click(
		object sender,
		RoutedEventArgs eventArgs)=>
		await OscilloscopeInstrumentPanel.SaveCsvAsync([2]);

	private async void ScopeSaveBothClick(
		object sender,
		RoutedEventArgs eventArgs)=>
		await OscilloscopeInstrumentPanel.SaveCsvAsync([1,2]);

	private async void GeneratorScanClick(
		object sender,
		RoutedEventArgs eventArgs)=>
		await GeneratorInstrumentPanel.ScanNetworkAsync();

	private void GeneratorAutoConnectClick(
		object sender,
		RoutedEventArgs eventArgs)=>
		GeneratorInstrumentPanel.AutoConnect=
			GeneratorAutoConnectMenuItem.IsChecked == true;

	private async void MultimeterScanClick(
		object sender,
		RoutedEventArgs eventArgs)=>
		await MultimeterInstrumentPanel.ScanNetworkAsync();

	private void MultimeterAutoConnectClick(
		object sender,
		RoutedEventArgs eventArgs)=>
		MultimeterInstrumentPanel.AutoConnect=
			MultimeterAutoConnectMenuItem.IsChecked == true;

	private void MultimeterSaveClick(
		object sender,
		RoutedEventArgs eventArgs)=>MultimeterInstrumentPanel.ExportCsv();

	private async void LoadScanClick(
		object sender,
		RoutedEventArgs eventArgs)=>
		await LoadInstrumentPanel.ScanNetworkAsync();

	private void LoadAutoConnectClick(
		object sender,
		RoutedEventArgs eventArgs)=>
		LoadInstrumentPanel.AutoConnect=
			LoadAutoConnectMenuItem.IsChecked == true;

	protected override async void OnClosing(CancelEventArgs eventArgs)
	{
		if(closeCompleted)
		{
			base.OnClosing(eventArgs);
			return;
		}
		eventArgs.Cancel=true;
		DetachEvents();
		await Task.WhenAll(
			KoradInstrumentPanel.DisposeAsync().AsTask(),
			OscilloscopeInstrumentPanel.DisposeAsync().AsTask(),
			GeneratorInstrumentPanel.DisposeAsync().AsTask(),
			MultimeterInstrumentPanel.DisposeAsync().AsTask(),
			LoadInstrumentPanel.DisposeAsync().AsTask());
		closeCompleted=true;
		if(!Dispatcher.HasShutdownStarted)
		{
			await Dispatcher.InvokeAsync(Close);
		}
	}

	private void DetachEvents()
	{
		KoradInstrumentPanel.CommandStateChanged-=PanelCommandStateChanged;
		OscilloscopeInstrumentPanel.CommandStateChanged-=PanelCommandStateChanged;
		GeneratorInstrumentPanel.CommandStateChanged-=PanelCommandStateChanged;
		MultimeterInstrumentPanel.CommandStateChanged-=PanelCommandStateChanged;
		LoadInstrumentPanel.CommandStateChanged-=PanelCommandStateChanged;
	}
}
