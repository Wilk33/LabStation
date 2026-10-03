using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LabStation.Instruments.Discovery;
using LabStation.Instruments.Transport;
using Microsoft.Win32;
using Sdm3000.Core;

namespace Sdm3000.App;

public partial class MultimeterView : UserControl,IAsyncDisposable
{
	private readonly IInstrumentNetworkScanner networkScanner;
	private readonly Func<string,IInstrumentTransport> transportFactory;
	private readonly IMultimeterSettingsStore settingsStore;
	private readonly TimeSpan refreshInterval;
	private readonly List<MeasurementSnapshot> recordedSnapshots=[];
	private MultimeterSession? session;
	private CancellationTokenSource? scanCancellation;
	private CancellationTokenSource? pollCancellation;
	private Task? pollTask;
	private bool connecting;
	private bool closing;
	private bool disposed;
	private bool autoConnect;
	private bool loadedHandled;

	private static string SettingsPath=>Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		AppInformation.DataDirectoryName,
		"settings.json");

	public MultimeterView()
		:this(
			new InstrumentNetworkScanner(),
			host=>new Vxi11Transport(host),
			new JsonMultimeterSettingsStore(
				SettingsPath,
				new("192.168.200.131",false)),
			TimeSpan.FromMilliseconds(100))
	{
	}

	public MultimeterView(
		IInstrumentNetworkScanner networkScanner,
		Func<string,IInstrumentTransport> transportFactory,
		IMultimeterSettingsStore settingsStore,
		TimeSpan refreshInterval)
	{
		this.networkScanner=networkScanner ?? throw new ArgumentNullException(nameof(networkScanner));
		this.transportFactory=transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
		this.settingsStore=settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
		if(refreshInterval <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(refreshInterval));
		}
		this.refreshInterval=refreshInterval;
		InitializeComponent();
		MultimeterSettings settings=settingsStore.Load();
		HostEditor.Text=settings.Address;
		autoConnect=settings.AutoConnect;
		Loaded+=MultimeterViewLoaded;
		Unloaded+=MultimeterViewUnloaded;
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
	public bool CanExport=>recordedSnapshots.Count>0;

	public void ExportCsv()
	{
		if(!CanExport)
		{
			return;
		}
		SaveFileDialog dialog=new()
		{
			Filter="CSV (*.csv)|*.csv",
			DefaultExt=".csv",
			AddExtension=true,
			FileName="SDM3000-odczyty-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".csv"
		};
		if(dialog.ShowDialog(Window.GetWindow(this)) != true)
		{
			return;
		}
		using StreamWriter writer=new(
			dialog.FileName,
			false,
			new UTF8Encoding(true));
		MeasurementCsvWriter.Write(writer,recordedSnapshots);
		ShowStatus("ZAPISANO CSV",false);
	}

	private async void MultimeterViewLoaded(object sender,RoutedEventArgs eventArgs)
	{
		if(loadedHandled)
		{
			return;
		}
		loadedHandled=true;
		if(AutoConnect && HostEditor.Text.Trim().Length > 0)
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
	private async void FunctionClick(object sender,RoutedEventArgs eventArgs)
	{
		if(sender is not Button button ||
			button.Tag is not string tag ||
			!Enum.TryParse(tag,out MeasurementFunction function) ||
			session is not MultimeterSession connected)
		{
			return;
		}
		FunctionButtons.IsEnabled=false;
		try
		{
			await connected.ConfigureAsync(function);
			MeasurementSnapshot snapshot=await connected.ReadAsync();
			if(session == connected && !closing)
			{
				ApplySnapshot(snapshot);
				ShowStatus("ONLINE",false);
			}
		}
		catch(Exception exception)
		{
			ShowStatus("BŁĄD KONFIGURACJI - "+exception.Message,true);
		}
		finally
		{
			UpdateEnabled();
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
			ShowStatus("Wprowadź adres IP multimetru.",true);
			return;
		}
		connecting=true;
		UpdateEnabled();
		ShowStatus("ŁĄCZENIE",false);
		try
		{
			MultimeterSession connected=await MultimeterSession.CreateAsync(
				()=>transportFactory(host));
			try
			{
				MeasurementSnapshot snapshot=await connected.ReadAsync();
				if(closing)
				{
					await connected.DisposeAsync();
					return;
				}
				session=connected;
				ApplySnapshot(snapshot);
				IdentityText.Text=connected.Identity.Manufacturer+" "+connected.Identity.Model;
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

	private void StartPolling(MultimeterSession connected)
	{
		pollCancellation?.Cancel();
		pollCancellation?.Dispose();
		pollCancellation=new CancellationTokenSource();
		pollTask=PollAsync(connected,pollCancellation.Token);
	}

	private async Task PollAsync(MultimeterSession connected,CancellationToken cancellationToken)
	{
		bool communicationError=false;
		while(!cancellationToken.IsCancellationRequested)
		{
			try
			{
				MeasurementSnapshot snapshot=await connected.ReadAsync();
				if(cancellationToken.IsCancellationRequested)
				{
					return;
				}
				await Dispatcher.InvokeAsync(()=>
				{
					ApplySnapshot(snapshot);
					if(communicationError)
					{
						ShowStatus("ONLINE",false);
						communicationError=false;
					}
				});
				await Task.Delay(refreshInterval,cancellationToken);
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
					await Dispatcher.InvokeAsync(()=>
						ShowStatus("BŁĄD KOMUNIKACJI - "+exception.Message,true));
				}
				await Task.Delay(refreshInterval,cancellationToken);
			}
		}
	}

	private async Task DisconnectAsync()
	{
		MultimeterSession? current=session;
		session=null;
		await StopPollingAsync();
		if(current is not null)
		{
			await current.DisposeAsync();
		}
		IdentityText.Text="";
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
				SiglentMultimeterClient.IsSupported,
				scanCancellation.Token);
			if(discovered is not null && !closing)
			{
				HostEditor.Text=discovered.Address;
				SaveSettings();
				ShowStatus("ZNALEZIONO "+discovered.Address,false);
			}
			else if(!closing)
			{
				ShowStatus("NIE ZNALEZIONO MULTIMETRU",false);
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

	private void ApplySnapshot(MeasurementSnapshot snapshot)
	{
		bool wasEmpty=recordedSnapshots.Count == 0;
		recordedSnapshots.Add(snapshot);
		if(recordedSnapshots.Count>100000)
		{
			recordedSnapshots.RemoveRange(0,recordedSnapshots.Count-100000);
		}
		MeasurementProfile profile=MeasurementProfiles.For(snapshot.Configuration.Function);
		FunctionNameText.Text=profile.Name;
		FunctionShortText.Text=profile.ShortName;
		PrimaryLabelText.Text=profile.PrimaryLabel;
		PrimaryValueText.Text=MeasurementFormatter.FormatValue(snapshot.Reading,profile.Unit);
		RangeText.Text=MeasurementFormatter.FormatRange(snapshot.Configuration.Range,profile.Unit);
		LocalStatisticsSnapshot statistics=snapshot.Statistics;
		MinimumText.Text=MeasurementFormatter.FormatStatistic(statistics.Minimum,profile.Unit);
		MaximumText.Text=MeasurementFormatter.FormatStatistic(statistics.Maximum,profile.Unit);
		AverageText.Text=MeasurementFormatter.FormatStatistic(statistics.Average,profile.Unit);
		PeakToPeakText.Text=MeasurementFormatter.FormatStatistic(statistics.PeakToPeak,profile.Unit);
		DeviationText.Text=MeasurementFormatter.FormatStatistic(statistics.StandardDeviation,profile.Unit);
		StatisticMinimumLabel.Text=profile.PrimaryLabel+" min";
		StatisticMaximumLabel.Text=profile.PrimaryLabel+" max";
		StatisticAverageLabel.Text=profile.PrimaryLabel+" avg";
		StatisticPeakToPeakLabel.Text=profile.PrimaryLabel+" P-P serii";
		StatisticDeviationLabel.Text="σ "+profile.PrimaryLabel;
		SeriesText.Text="Seria: "+statistics.Count+" odczytów | Pamięć: "+snapshot.StoredPoints;
		if(wasEmpty)
		{
			CommandStateChanged?.Invoke(this,EventArgs.Empty);
		}
	}

	private async void MultimeterViewUnloaded(object? sender,EventArgs eventArgs)
	{
		await DisposeAsync();
	}

	private void UpdateEnabled()
	{
		bool connected=session is not null;
		ConnectionButton.Content=connected ? "Online" : "Offline";
		ConnectionButton.IsEnabled=!connecting && !closing;
		HostEditor.IsEnabled=!connected && !connecting && !closing;
		FunctionButtons.IsEnabled=connected && !connecting && !closing;
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
		Loaded-=MultimeterViewLoaded;
		Unloaded-=MultimeterViewUnloaded;
		scanCancellation?.Cancel();
		UpdateEnabled();
		await StopPollingAsync();
		MultimeterSession? current=session;
		session=null;
		if(current is not null)
		{
			await current.DisposeAsync();
		}
		scanCancellation?.Dispose();
	}
}
