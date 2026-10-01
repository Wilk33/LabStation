using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LabStation.Instruments.Discovery;
using LabStation.Instruments.Transport;
using Sdg1032X.Core;

namespace Sdg1032X.App;

public partial class GeneratorView : UserControl,IAsyncDisposable
{
	private readonly IInstrumentNetworkScanner networkScanner;
	private readonly Func<string,IInstrumentTransport> transportFactory;
	private readonly IGeneratorSettingsStore settingsStore;
	private GeneratorSession? session;
	private CancellationTokenSource? scanCancellation;
	private bool connecting;
	private bool closing;
	private bool disposed;
	private bool autoConnect;
	private bool loadedHandled;

	private static string SettingsPath=>Path.Combine(
		Environment.GetFolderPath(
			Environment.SpecialFolder.LocalApplicationData),
		ProductInformation.DataDirectoryName,
		"settings.json");

	public GeneratorView()
		:this(
			new InstrumentNetworkScanner(),
			host=>new Vxi11Transport(host),
			new JsonGeneratorSettingsStore(
				SettingsPath,
				new("192.168.200.132",false)))
	{
	}

	public GeneratorView(
		IInstrumentNetworkScanner networkScanner,
		Func<string,IInstrumentTransport> transportFactory,
		IGeneratorSettingsStore settingsStore)
	{
		this.networkScanner=networkScanner ??
			throw new ArgumentNullException(nameof(networkScanner));
		this.transportFactory=transportFactory ??
			throw new ArgumentNullException(nameof(transportFactory));
		this.settingsStore=settingsStore ??
			throw new ArgumentNullException(nameof(settingsStore));
		InitializeComponent();
		Channel1.Configure(1,()=>session,ShowStatus);
		Channel2.Configure(2,()=>session,ShowStatus);
		Channel1.SummaryChanged+=
			(_, eventArgs)=>Channel1Header.Text=eventArgs.Text;
		Channel2.SummaryChanged+=
			(_, eventArgs)=>Channel2Header.Text=eventArgs.Text;
		GeneratorSettings settings=settingsStore.Load();
		HostEditor.Text=settings.Address;
		autoConnect=settings.AutoConnect;
		SetControlsEnabled(false);
		Loaded+=GeneratorViewLoaded;
		Unloaded+=GeneratorViewUnloaded;
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

	public bool CanScanNetwork=>
		session is null && !connecting && !closing;

	private async void GeneratorViewLoaded(
		object sender,
		RoutedEventArgs eventArgs)
	{
		if(loadedHandled)
		{
			return;
		}
		loadedHandled=true;
		if(AutoConnect && HostEditor.Text.Trim().Length>0)
		{
			await ConnectAsync();
		}
	}

	private async void ConnectionClick(
		object sender,
		RoutedEventArgs eventArgs)
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

	private async Task ConnectAsync()
	{
		if(connecting || closing || session is not null)
		{
			return;
		}
		string host=HostEditor.Text.Trim();
		if(string.IsNullOrWhiteSpace(host))
		{
			ShowStatus("Wprowadź adres IP generatora.",true);
			return;
		}
		connecting=true;
		UpdateEnabled();
		ShowStatus("ŁĄCZENIE",false);
		try
		{
			GeneratorSession connected=await GeneratorSession.CreateAsync(
				()=>transportFactory(host));
			connected.CommunicationFailed+=SessionCommunicationFailed;
			try
			{
				ChannelSnapshot[] snapshots=await Task.WhenAll(
					connected.ReadChannelAsync(1),
					connected.ReadChannelAsync(2));
				if(closing)
				{
					await DisposeSessionAsync(connected);
					return;
				}
				session=connected;
				Channel1.ApplySnapshot(snapshots[0]);
				Channel2.ApplySnapshot(snapshots[1]);
				SetControlsEnabled(true);
				SaveSettings();
				ShowStatus("ONLINE",false);
			}
			catch
			{
				await DisposeSessionAsync(connected);
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

	private async Task DisconnectAsync()
	{
		GeneratorSession? current=session;
		session=null;
		SetControlsEnabled(false);
		UpdateEnabled();
		if(current is not null)
		{
			await DisposeSessionAsync(current);
		}
		ShowStatus("OFFLINE",false);
		UpdateEnabled();
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
				SiglentGeneratorClient.IsSupported,
				scanCancellation.Token);
			if(discovered is not null && !closing)
			{
				HostEditor.Text=discovered.Address;
				SaveSettings();
				ShowStatus("ZNALEZIONO "+discovered.Address,false);
			}
			else if(!closing)
			{
				ShowStatus("NIE ZNALEZIONO GENERATORA",false);
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

	private async void GeneratorViewUnloaded(
		object? sender,
		EventArgs eventArgs)
	{
		await DisposeAsync();
	}

	private void SessionCommunicationFailed(
		object? sender,
		Exception exception)
	{
		if(Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
		{
			return;
		}
		if(Dispatcher.CheckAccess())
		{
			ShowStatus("BŁĄD KOMUNIKACJI - "+exception.Message,true);
			return;
		}
		Dispatcher.InvokeAsync(()=>
			ShowStatus("BŁĄD KOMUNIKACJI - "+exception.Message,true));
	}

	private async Task DisposeSessionAsync(
		GeneratorSession current)
	{
		current.CommunicationFailed-=SessionCommunicationFailed;
		await current.DisposeAsync();
	}

	private void SetControlsEnabled(bool enabled)
	{
		Channel1.IsEnabled=enabled;
		Channel2.IsEnabled=enabled;
	}

	private void UpdateEnabled()
	{
		bool connected=session is not null;
		ConnectionButton.Content=connected ? "Online" : "Offline";
		ConnectionButton.IsEnabled=!connecting && !closing;
		HostEditor.IsEnabled=!connected && !connecting && !closing;
		CommandStateChanged?.Invoke(this,EventArgs.Empty);
	}

	private void ShowStatus(string message,bool error)
	{
		StatusText.Text=message.StartsWith(
			"Status:",
			StringComparison.OrdinalIgnoreCase)
			? message
			: "Status: "+message;
		StatusText.Foreground=error
			? new SolidColorBrush(Color.FromRgb(255,128,128))
			: (Brush)FindResource("LabStationTextBrush");
	}

	private void SaveSettings()
	{
		settingsStore.Save(new(HostEditor.Text.Trim(),AutoConnect));
	}

	public async ValueTask DisposeAsync()
	{
		if(disposed)
		{
			return;
		}
		disposed=true;
		closing=true;
		Loaded-=GeneratorViewLoaded;
		Unloaded-=GeneratorViewUnloaded;
		scanCancellation?.Cancel();
		UpdateEnabled();
		while(connecting)
		{
			await Task.Delay(50);
		}
		GeneratorSession? current=session;
		session=null;
		if(current is not null)
		{
			current.CommunicationFailed-=SessionCommunicationFailed;
			await current.DisposeAsync();
		}
		scanCancellation?.Dispose();
	}
}
