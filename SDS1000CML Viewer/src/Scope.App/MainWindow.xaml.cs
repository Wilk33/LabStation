using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using LabStation.UI;

namespace Scope.App;

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

	private void InstrumentPanelCommandStateChanged(
		object? sender,
		EventArgs eventArgs)
	{
		UpdateMenuState();
	}

	private void UpdateMenuState()
	{
		SetEnabled(ScanNetworkMenuItem,InstrumentPanel.CanScanNetwork);
		if(AutoConnectMenuItem.IsChecked != InstrumentPanel.AutoConnect)
		{
			AutoConnectMenuItem.IsChecked=InstrumentPanel.AutoConnect;
		}
		SetEnabled(SaveChannel1MenuItem,InstrumentPanel.CanSaveChannel1);
		SetEnabled(SaveChannel2MenuItem,InstrumentPanel.CanSaveChannel2);
		SetEnabled(
			SaveBothChannelsMenuItem,
			InstrumentPanel.CanSaveBothChannels);
	}

	private static void SetEnabled(MenuItem item,bool enabled)
	{
		if(item.IsEnabled != enabled)
		{
			item.IsEnabled=enabled;
		}
	}

	private async void ScanNetworkClick(
		object sender,
		RoutedEventArgs eventArgs)
	{
		await InstrumentPanel.ScanNetworkAsync();
	}

	private void AutoConnectClick(object sender,RoutedEventArgs eventArgs)
	{
		InstrumentPanel.AutoConnect=AutoConnectMenuItem.IsChecked;
	}

	private async void SaveChannel1Click(
		object sender,
		RoutedEventArgs eventArgs)
	{
		await InstrumentPanel.SaveCsvAsync([1]);
	}

	private async void SaveChannel2Click(
		object sender,
		RoutedEventArgs eventArgs)
	{
		await InstrumentPanel.SaveCsvAsync([2]);
	}

	private async void SaveBothChannelsClick(
		object sender,
		RoutedEventArgs eventArgs)
	{
		await InstrumentPanel.SaveCsvAsync([1,2]);
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
