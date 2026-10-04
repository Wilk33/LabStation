using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LabStation.Instruments.Discovery;
using LabStation.Instruments.Transport;
using LabStation.UI.Controls;
using LabStation.UI;
using Sdl1000X.Core;

namespace Sdl1000X.App;

public partial class ElectronicLoadView : UserControl,IAsyncDisposable
{
	private readonly IInstrumentNetworkScanner networkScanner;
	private readonly Func<string,IInstrumentTransport> transportFactory;
	private readonly IElectronicLoadSettingsStore settingsStore;
	private readonly TimeSpan refreshInterval;
	private ElectronicLoadSession? session;
	private ElectronicLoadSnapshot? currentSnapshot;
	private CancellationTokenSource? scanCancellation;
	private CancellationTokenSource? pollCancellation;
	private Task? pollTask;
	private bool connecting;
	private bool closing;
	private bool disposed;
	private bool autoConnect;
	private bool applyingSnapshot;
	private bool loadedHandled;

	private static string SettingsPath=>Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		AppInformation.DataDirectoryName,
		"settings.json");

	public ElectronicLoadView()
		:this(
			new InstrumentNetworkScanner(),
			host=>new Vxi11Transport(host),
			new JsonElectronicLoadSettingsStore(SettingsPath,new("",false)),
			TimeSpan.FromMilliseconds(500))
	{
	}

	public ElectronicLoadView(
		IInstrumentNetworkScanner networkScanner,
		Func<string,IInstrumentTransport> transportFactory,
		IElectronicLoadSettingsStore settingsStore,
		TimeSpan refreshInterval)
	{
		this.networkScanner=networkScanner ?? throw new ArgumentNullException(nameof(networkScanner));
		this.transportFactory=transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
		this.settingsStore=settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
		if(refreshInterval<=TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(refreshInterval));
		}
		this.refreshInterval=refreshInterval;
		InitializeComponent();
		ElectronicLoadSettings settings=settingsStore.Load();
		HostEditor.Text=settings.Address;
		autoConnect=settings.AutoConnect;
		Loaded+=ElectronicLoadViewLoaded;
		Unloaded+=ElectronicLoadViewUnloaded;
		SelectModeButton(LoadMode.ConstantCurrent);
		UpdateEnabled();
	}

	public event EventHandler? CommandStateChanged;

	public bool AutoConnect
	{
		get=>autoConnect;
		set
		{
			if(autoConnect == value)
			{
				return;
			}
			autoConnect=value;
			SaveSettings();
			CommandStateChanged?.Invoke(this,EventArgs.Empty);
		}
	}

	public bool CanScanNetwork=>session is null && !connecting && !closing;
	public bool IsConnected=>session is not null;
	public string HostAddress=>HostEditor.Text.Trim();
	public ElectronicLoadSnapshot? CurrentSnapshot=>currentSnapshot;

	private async void ElectronicLoadViewLoaded(object sender,RoutedEventArgs eventArgs)
	{
		if(loadedHandled)
		{
			return;
		}
		loadedHandled=true;
		if(StartupPolicy.AutoConnectAllowed &&
			AutoConnect &&
			HostAddress.Length>0)
		{
			await ConnectAsync();
		}
	}

	private async void ConnectionClick(object sender,RoutedEventArgs eventArgs)
	{
		if(session is null)
		{
			await ConnectAsync();
		}
		else
		{
			await DisconnectAsync();
		}
	}

	private async void ModeClick(object sender,RoutedEventArgs eventArgs)
	{
		if(sender is not Button button ||
			button.Tag is not string value ||
			!Enum.TryParse(value,out LoadMode mode) ||
			session is not ElectronicLoadSession connected)
		{
			return;
		}
		try
		{
			await connected.SetModeAsync(mode);
			ApplySnapshot(await connected.ReadAsync());
			ShowStatus("ONLINE",false);
		}
		catch(TaskCanceledException)
		{
		}
		catch(Exception exception)
		{
			ShowStatus("BŁĄD NASTAWY - "+exception.Message,true);
		}
	}

	private void SetpointCommitted(object sender,NumericValueCommittedEventArgs eventArgs)
	{
		if(applyingSnapshot ||
			currentSnapshot is null ||
			currentSnapshot.Mode == LoadMode.Led ||
			session is not ElectronicLoadSession connected)
		{
			return;
		}
		_ = ObserveWriteAsync(
			connected.SetSetpointAsync(currentSnapshot.Mode,eventArgs.Value),
			"USTAWIONO "+LoadModeProfiles.For(currentSnapshot.Mode).Name);
	}

	private void LedSettingCommitted(object sender,NumericValueCommittedEventArgs eventArgs)
	{
		if(applyingSnapshot || session is not ElectronicLoadSession connected)
		{
			return;
		}
		_ = ObserveWriteAsync(
			connected.SetLedSettingsAsync(new(
				LedVoltageEditor.Value,
				LedCurrentEditor.Value,
				LedResistanceEditor.Value)),
			"USTAWIONO LED");
	}

	private void ProtectionCommitted(object sender,NumericValueCommittedEventArgs eventArgs)
	{
		CommitProtections();
	}

	private void ProtectionStateClick(object sender,RoutedEventArgs eventArgs)
	{
		CommitProtections();
	}

	private void CommitProtections()
	{
		if(applyingSnapshot || session is not ElectronicLoadSession connected)
		{
			return;
		}
		_ = ObserveWriteAsync(
			connected.SetProtectionsAsync(new(
				OverCurrentEnabled.IsChecked == true,
				OverCurrentEditor.Value,
				OverPowerEnabled.IsChecked == true,
				OverPowerEditor.Value)),
			"USTAWIONO ZABEZPIECZENIA");
	}

	private async void InputClick(object sender,RoutedEventArgs eventArgs)
	{
		if(session is not ElectronicLoadSession connected)
		{
			return;
		}
		bool enable=currentSnapshot?.InputEnabled != true;
		if(enable)
		{
			MessageBoxResult confirmation=MessageBox.Show(
				Window.GetWindow(this),
				BuildEnableConfirmation(),
				"Włącz wejście obciążenia",
				MessageBoxButton.YesNo,
				MessageBoxImage.Warning,
				MessageBoxResult.No);
			if(confirmation != MessageBoxResult.Yes)
			{
				return;
			}
		}
		InputButton.IsEnabled=false;
		try
		{
			await connected.SetInputAsync(enable);
			ApplySnapshot(await connected.ReadAsync());
			ShowStatus(enable ? "ON" : "OFF",false);
		}
		catch(Exception exception)
		{
			ShowStatus("BŁĄD WEJŚCIA - "+exception.Message,true);
		}
		finally
		{
			UpdateEnabled();
		}
	}

	public async Task ScanNetworkAsync()
	{
		if(!CanScanNetwork)
		{
			return;
		}
		scanCancellation?.Dispose();
		scanCancellation=new CancellationTokenSource();
		DiscoveredInstrument? discovered=null;
		connecting=true;
		UpdateEnabled();
		ShowStatus("SKANOWANIE SIECI",false);
		try
		{
			discovered=await networkScanner.FindFirstAsync(
				SiglentElectronicLoadClient.IsSupported,
				scanCancellation.Token);
			if(discovered is not null && !closing)
			{
				HostEditor.Text=discovered.Address;
				SaveSettings();
				ShowStatus("ZNALEZIONO "+discovered.Address,false);
			}
			else if(!closing)
			{
				ShowStatus("NIE ZNALEZIONO SDL1020X-E",false);
			}
		}
		catch(OperationCanceledException)
		{
		}
		finally
		{
			connecting=false;
			UpdateEnabled();
		}
		if(discovered is not null && AutoConnect && !closing)
		{
			await ConnectAsync();
		}
	}

	private async Task ConnectAsync()
	{
		if(connecting || closing || session is not null)
		{
			return;
		}
		string host=HostAddress;
		if(string.IsNullOrWhiteSpace(host))
		{
			ShowStatus("Wprowadź adres IP obciążenia.",true);
			return;
		}
		connecting=true;
		UpdateEnabled();
		ShowStatus("ŁĄCZENIE",false);
		try
		{
			ElectronicLoadSession connected=await ElectronicLoadSession.CreateAsync(
				()=>transportFactory(host));
			try
			{
				ElectronicLoadSnapshot snapshot=await connected.ReadAsync();
				if(closing)
				{
					await connected.DisposeAsync();
					return;
				}
				session=connected;
				ApplySnapshot(snapshot);
				SaveSettings();
				ShowStatus("ONLINE",false);
				StartPolling(connected);
			}
			catch
			{
				await connected.DisposeAsync();
				throw;
			}
		}
		catch(Exception exception)
		{
			ShowStatus("BŁĄD POŁĄCZENIA - "+exception.Message,true);
		}
		finally
		{
			connecting=false;
			UpdateEnabled();
		}
	}

	private void StartPolling(ElectronicLoadSession connected)
	{
		pollCancellation?.Cancel();
		pollCancellation?.Dispose();
		pollCancellation=new CancellationTokenSource();
		pollTask=PollAsync(connected,pollCancellation.Token);
	}

	private async Task PollAsync(
		ElectronicLoadSession connected,
		CancellationToken cancellationToken)
	{
		bool communicationError=false;
		while(!cancellationToken.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(refreshInterval,cancellationToken);
				ElectronicLoadSnapshot snapshot=await connected.ReadAsync();
				if(cancellationToken.IsCancellationRequested)
				{
					return;
				}
				await Dispatcher.InvokeAsync(() =>
				{
					ApplySnapshot(snapshot);
					if(communicationError)
					{
						ShowStatus("ONLINE",false);
						communicationError=false;
					}
				});
			}
			catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
			{
				return;
			}
			catch(Exception exception)
			{
				communicationError=true;
				if(!Dispatcher.HasShutdownStarted && !closing)
				{
					await Dispatcher.InvokeAsync(() =>
						ShowStatus("BŁĄD KOMUNIKACJI - "+exception.Message,true));
				}
			}
		}
	}

	private async Task ObserveWriteAsync(Task operation,string success)
	{
		try
		{
			await operation;
			ShowStatus(success,false);
		}
		catch(TaskCanceledException)
		{
		}
		catch(Exception exception)
		{
			ShowStatus("BŁĄD NASTAWY - "+exception.Message,true);
		}
	}

	private void ApplySnapshot(ElectronicLoadSnapshot snapshot)
	{
		currentSnapshot=snapshot;
		applyingSnapshot=true;
		try
		{
			VoltageValueText.Text=Format(snapshot.Measurements.Voltage,3);
			CurrentValueText.Text=Format(snapshot.Measurements.Current,3);
			PowerValueText.Text=Format(snapshot.Measurements.Power,2);
			ResistanceValueText.Text=Format(snapshot.Measurements.Resistance,3);
			InputButton.Content=snapshot.InputEnabled ? "ON" : "OFF";
			SelectModeButton(snapshot.Mode);
			if(snapshot.Mode == LoadMode.Led)
			{
				SetpointEditor.Visibility=Visibility.Collapsed;
				LedSettingsPanel.Visibility=Visibility.Visible;
				if(snapshot.Led is not null && !LedSettingsPanel.IsKeyboardFocusWithin)
				{
					LedVoltageEditor.Value=snapshot.Led.Voltage;
					LedCurrentEditor.Value=snapshot.Led.Current;
					LedResistanceEditor.Value=snapshot.Led.Resistance;
				}
			}
			else
			{
				SetpointEditor.Visibility=Visibility.Visible;
				LedSettingsPanel.Visibility=Visibility.Collapsed;
				LoadModeProfile profile=LoadModeProfiles.For(snapshot.Mode);
				SetpointEditor.Label="Nastawa "+profile.Name;
				SetpointEditor.Unit=profile.Unit;
				SetpointEditor.Minimum=profile.Minimum;
				SetpointEditor.Maximum=profile.Maximum;
				SetpointEditor.Step=profile.Step;
				SetpointEditor.DecimalPlaces=profile.DecimalPlaces;
				if(!SetpointEditor.IsKeyboardFocusWithin)
				{
					SetpointEditor.Value=snapshot.Setpoint ?? profile.Minimum;
				}
			}
			if(!OverCurrentEditor.IsKeyboardFocusWithin)
			{
				OverCurrentEditor.Value=snapshot.Protections.OverCurrentAmps;
			}
			if(!OverPowerEditor.IsKeyboardFocusWithin)
			{
				OverPowerEditor.Value=snapshot.Protections.OverPowerWatts;
			}
			OverCurrentEnabled.IsChecked=snapshot.Protections.OverCurrentEnabled;
			OverPowerEnabled.IsChecked=snapshot.Protections.OverPowerEnabled;
		}
		finally
		{
			applyingSnapshot=false;
		}
		UpdateEnabled();
	}

	private void SelectModeButton(LoadMode mode)
	{
		foreach(Button button in ModeButtons.Children.OfType<Button>())
		{
			bool selected=button.Tag?.ToString() == mode.ToString();
			if(selected)
			{
				button.Background=new SolidColorBrush(Color.FromRgb(255,220,50));
				button.Foreground=Brushes.Black;
			}
			else
			{
				button.ClearValue(BackgroundProperty);
				button.ClearValue(ForegroundProperty);
			}
		}
	}

	private string BuildEnableConfirmation()
	{
		if(currentSnapshot is null)
		{
			return "Włączyć wejście obciążenia?";
		}
		if(currentSnapshot.Mode == LoadMode.Led && currentSnapshot.Led is not null)
		{
			return string.Format(
				CultureInfo.CurrentCulture,
				"Włączyć wejście w trybie LED?\nVo: {0:G6} V, Io: {1:G6} A, Rco: {2:G6} Ω",
				currentSnapshot.Led.Voltage,
				currentSnapshot.Led.Current,
				currentSnapshot.Led.Resistance);
		}
		LoadModeProfile profile=LoadModeProfiles.For(currentSnapshot.Mode);
		return string.Format(
			CultureInfo.CurrentCulture,
			"Włączyć wejście w trybie {0}?\nNastawa: {1:G6} {2}",
			profile.Name,
			currentSnapshot.Setpoint ?? 0,
			profile.Unit);
	}

	private async Task DisconnectAsync()
	{
		ElectronicLoadSession? current=session;
		session=null;
		await StopPollingAsync();
		if(current is not null)
		{
			try
			{
				await current.DisposeAsync();
			}
			catch(Exception exception)
			{
				ShowStatus("BŁĄD WYŁĄCZANIA - "+exception.Message,true);
				UpdateEnabled();
				return;
			}
		}
		currentSnapshot=null;
		InputButton.Content="OFF";
		ShowStatus("OFFLINE",false);
		UpdateEnabled();
	}

	private async Task StopPollingAsync()
	{
		pollCancellation?.Cancel();
		if(pollTask is not null)
		{
			try
			{
				await pollTask;
			}
			catch(OperationCanceledException)
			{
			}
		}
		pollTask=null;
		pollCancellation?.Dispose();
		pollCancellation=null;
	}

	private void UpdateEnabled()
	{
		bool connected=session is not null;
		bool inputOn=currentSnapshot?.InputEnabled == true;
		ConnectionButton.Content=connected ? "Online" : "Offline";
		ConnectionButton.IsEnabled=!connecting && !closing;
		HostEditor.IsEnabled=!connected && !connecting && !closing;
		InputButton.IsEnabled=connected && !connecting && !closing;
		ModeButtons.IsEnabled=connected && !inputOn && !closing;
		SetpointEditor.IsEnabled=connected && !inputOn && !closing;
		LedSettingsPanel.IsEnabled=connected && !inputOn && !closing;
		OverCurrentEditor.IsEnabled=connected && !inputOn && !closing;
		OverPowerEditor.IsEnabled=connected && !inputOn && !closing;
		OverCurrentEnabled.IsEnabled=connected && !inputOn && !closing;
		OverPowerEnabled.IsEnabled=connected && !inputOn && !closing;
		CommandStateChanged?.Invoke(this,EventArgs.Empty);
	}

	private void ShowStatus(string message,bool error)
	{
		StatusText.Text=message.StartsWith("Status:",StringComparison.OrdinalIgnoreCase)
			? message
			: "Status: "+message;
		StatusText.Foreground=error
			? new SolidColorBrush(Color.FromRgb(255,128,128))
			: (Brush)FindResource("LabStationTextBrush");
	}

	private void SaveSettings()
	{
		settingsStore.Save(new(HostAddress,AutoConnect));
	}

	private static string Format(double value,int decimals)
	{
		return value.ToString("F"+decimals,CultureInfo.CurrentCulture);
	}

	private async void ElectronicLoadViewUnloaded(object? sender,EventArgs eventArgs)
	{
		await DisposeAsync();
	}

	public async ValueTask DisposeAsync()
	{
		if(disposed)
		{
			return;
		}
		disposed=true;
		closing=true;
		Loaded-=ElectronicLoadViewLoaded;
		Unloaded-=ElectronicLoadViewUnloaded;
		scanCancellation?.Cancel();
		UpdateEnabled();
		await StopPollingAsync();
		ElectronicLoadSession? current=session;
		session=null;
		if(current is not null)
		{
			try
			{
				await current.DisposeAsync();
			}
			catch(Exception exception)
			{
				ShowStatus("BŁĄD WYŁĄCZANIA - "+exception.Message,true);
			}
		}
		scanCancellation?.Dispose();
	}
}
