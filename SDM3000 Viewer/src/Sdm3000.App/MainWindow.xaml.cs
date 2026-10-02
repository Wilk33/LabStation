using System.ComponentModel;
using System.Windows;
using LabStation.UI;

namespace Sdm3000.App;

public partial class MainWindow : Window
{
	private bool closeCompleted;

	public MainWindow()
	{
		InitializeComponent();
		Title=AppInformation.DisplayName;
		SystemTheme.ApplyTo(this);
		InstrumentPanel.CommandStateChanged+=InstrumentPanelCommandStateChanged;
		UpdateMenuState();
	}

	private void InstrumentPanelCommandStateChanged(object? sender,EventArgs eventArgs)
	{
		UpdateMenuState();
	}

	private void UpdateMenuState()
	{
		ScanNetworkMenuItem.IsEnabled=InstrumentPanel.CanScanNetwork;
		AutoConnectMenuItem.IsChecked=InstrumentPanel.AutoConnect;
	}

	private async void ScanNetworkClick(object sender,RoutedEventArgs eventArgs)
	{
		await InstrumentPanel.ScanNetworkAsync();
	}

	private void AutoConnectClick(object sender,RoutedEventArgs eventArgs)
	{
		InstrumentPanel.AutoConnect=AutoConnectMenuItem.IsChecked == true;
	}

	protected override async void OnClosing(CancelEventArgs eventArgs)
	{
		if(closeCompleted)
		{
			base.OnClosing(eventArgs);
			return;
		}
		eventArgs.Cancel=true;
		InstrumentPanel.CommandStateChanged-=InstrumentPanelCommandStateChanged;
		await InstrumentPanel.DisposeAsync();
		closeCompleted=true;
		if(!Dispatcher.HasShutdownStarted)
		{
			await Dispatcher.InvokeAsync(Close);
		}
	}
}
