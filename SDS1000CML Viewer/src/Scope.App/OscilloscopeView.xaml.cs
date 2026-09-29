using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Scope.Core;

namespace Scope.App;

public partial class OscilloscopeView : UserControl,IAsyncDisposable
{
	private static readonly Brush[] CursorBrushes=
	[
		Brushes.OrangeRed,
		Brushes.LimeGreen,
		Brushes.DeepSkyBlue,
		Brushes.Magenta
	];
	private readonly DispatcherTimer timer=new()
	{
		Interval=TimeSpan.FromMilliseconds(750)
	};
	private readonly InstrumentOperationQueue operations=new();
	private ScopeClient? scope;
	private Waveform[]? captured;
	private ChannelMeasurements[] lastMeasurements=[];
	private bool busy;
	private bool commandPending;
	private bool closing;
	private bool disposed;

	private int[] Channels=>
	[
		..new[]
		{
			Channel1CheckBox.IsChecked == true ? 1 : 0,
			Channel2CheckBox.IsChecked == true ? 2 : 0
		}.Where(channel=>channel != 0)
	];

	private static string SettingsPath=>Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		AppInformation.DataDirectoryName,
		"settings.json");

	public OscilloscopeView()
	{
		InitializeComponent();
		Plot.CursorStateChanged+=OnCursorStateChanged;
		timer.Tick+=OnTimerTick;
		SetCursorToolTips();
		LoadSettings();
		ChannelSelectionChanged(this,new RoutedEventArgs());
		timer.Start();
	}

	private Button[] CursorButtons=>
	[
		Cursor1Button,
		Cursor2Button,
		Cursor3Button,
		Cursor4Button
	];

	private void SetCursorToolTips()
	{
		Button[] buttons=CursorButtons;
		for(int index=0;index < buttons.Length;index++)
		{
			buttons[index].ToolTip=
				$"Kursor {index+1}: kliknij, aby włączyć lub wybrać. Ponowne kliknięcie wybranego kursora wyłącza go.";
		}
	}

	private async void OnTimerTick(object? sender,EventArgs eventArgs)
	{
		if(!busy &&
			!commandPending &&
			scope is not null &&
			LiveCheckBox.IsChecked == true &&
			Channels.Length > 0)
		{
			await CapturePreview();
		}
	}

	private async void ConnectOrDisconnectClick(
		object sender,
		RoutedEventArgs eventArgs)
	{
		await ConnectOrDisconnect();
	}

	private async void StartClick(object sender,RoutedEventArgs eventArgs)
	{
		await Command(client=>client.Start());
	}

	private async void StopClick(object sender,RoutedEventArgs eventArgs)
	{
		await Command(client=>client.Stop());
	}

	private async void AutoClick(object sender,RoutedEventArgs eventArgs)
	{
		await Command(client=>client.Auto());
	}

	private async void CaptureClick(object sender,RoutedEventArgs eventArgs)
	{
		await CaptureManualWaveform();
	}

	private async void SaveClick(object sender,RoutedEventArgs eventArgs)
	{
		await SaveCsv();
	}

	private void Cursor1Click(object sender,RoutedEventArgs eventArgs)
	{
		Plot.ActivateOrSelectCursor(0);
	}

	private void Cursor2Click(object sender,RoutedEventArgs eventArgs)
	{
		Plot.ActivateOrSelectCursor(1);
	}

	private void Cursor3Click(object sender,RoutedEventArgs eventArgs)
	{
		Plot.ActivateOrSelectCursor(2);
	}

	private void Cursor4Click(object sender,RoutedEventArgs eventArgs)
	{
		Plot.ActivateOrSelectCursor(3);
	}

	private void OnCursorStateChanged(object? sender,EventArgs eventArgs)
	{
		UpdateCursorButtons();
	}

	private void ChannelSelectionChanged(
		object sender,
		RoutedEventArgs eventArgs)
	{
		if(!IsInitialized)
		{
			return;
		}
		int[] channels=Channels;
		Plot.SetVisibleChannels(channels);
		Channel1Measurements.Visibility=Channel1CheckBox.IsChecked == true
			? Visibility.Visible
			: Visibility.Hidden;
		Channel2Measurements.Visibility=Channel2CheckBox.IsChecked == true
			? Visibility.Visible
			: Visibility.Hidden;
		DetailText.Visibility=channels.Length > 0
			? Visibility.Visible
			: Visibility.Hidden;
		UpdateCursorButtons();
		UpdateEnabled();
	}

	private void UpdateEnabled()
	{
		bool connected=scope is not null;
		ConnectionButton.IsEnabled=connected
			? !commandPending && !closing
			: !busy && !commandPending && !closing;
		ConnectionButton.Content=connected ? "Online" : "Offline";
		AddressTextBox.IsEnabled=!busy && !connected && !closing;
		StartButton.IsEnabled=connected && !commandPending && !closing;
		StopButton.IsEnabled=connected && !commandPending && !closing;
		AutoButton.IsEnabled=connected && !commandPending && !closing;
		CaptureButton.IsEnabled=
			connected && !commandPending && Channels.Length > 0 && !closing;
		SaveButton.IsEnabled=
			captured is not null && !busy && !commandPending && !closing;
		foreach(Button cursorButton in CursorButtons)
		{
			cursorButton.IsEnabled=Plot.HasWaveforms && !closing;
		}
	}

	private async Task ConnectOrDisconnect()
	{
		if(scope is not null)
		{
			if(commandPending || closing)
			{
				return;
			}
			commandPending=true;
			UpdateEnabled();
			try
			{
				await Disconnect();
			}
			catch(Exception exception)
			{
				ShowError(exception,true);
			}
			finally
			{
				commandPending=false;
				UpdateEnabled();
			}
			return;
		}
		if(busy || commandPending || closing)
		{
			return;
		}
		busy=true;
		UpdateEnabled();
		try
		{
			string host=AddressTextBox.Text.Trim();
			if(host.Length == 0)
			{
				throw new InvalidOperationException("Wpisz adres IP oscyloskopu.");
			}
			scope=await Task.Run(()=>
			{
				IInstrumentTransport transport=new Vxi11Transport(host);
				ScopeClient client=new(transport);
				try
				{
					client.Initialize();
					return client;
				}
				catch
				{
					client.Dispose();
					throw;
				}
			});
			AcquisitionState state=await Task.Run(scope.AcquisitionStatus);
			UpdateAcquisition(state);
			SaveSettings();
		}
		catch(Exception exception)
		{
			ShowError(exception,true);
		}
		finally
		{
			busy=false;
			UpdateEnabled();
		}
	}

	private async Task Disconnect()
	{
		ScopeClient? old=scope;
		scope=null;
		Plot.Stale=true;
		Plot.InvalidateVisual();
		UpdateMeasurementRows(lastMeasurements,true);
		if(old is not null)
		{
			await operations.RunCommandAsync(()=>Task.Run(old.Dispose));
		}
		UpdateAcquisition(AcquisitionState.Unknown);
	}

	private async Task Command(Action<ScopeClient> action)
	{
		if(commandPending || scope is null)
		{
			return;
		}
		commandPending=true;
		UpdateEnabled();
		ScopeClient client=scope;
		try
		{
			AcquisitionState state=AcquisitionState.Unknown;
			await operations.RunCommandAsync(async()=>
			{
				await Task.Run(()=>
				{
					action(client);
					state=client.AcquisitionStatus();
				});
			});
			if(ReferenceEquals(scope,client))
			{
				UpdateAcquisition(state);
			}
		}
		catch(Exception exception)
		{
			if(ReferenceEquals(scope,client))
			{
				await Disconnect();
			}
			ShowError(exception,true);
		}
		finally
		{
			commandPending=false;
			UpdateEnabled();
		}
	}

	private sealed record ScopeSnapshot(
		Waveform[] Waves,
		ChannelMeasurements[] Measurements,
		AcquisitionState State);

	private async Task CaptureManualWaveform()
	{
		if(commandPending || scope is null)
		{
			return;
		}
		int[] channels=Channels;
		if(channels.Length == 0)
		{
			return;
		}
		commandPending=true;
		UpdateEnabled();
		ScopeClient client=scope;
		try
		{
			ScopeSnapshot? snapshot=null;
			await operations.RunCommandAsync(async()=>
			{
				snapshot=await Task.Run(()=>ReadSnapshot(client,channels));
			});
			if(snapshot is not null && ReferenceEquals(scope,client))
			{
				ApplySnapshot(snapshot,true);
			}
		}
		catch(InvalidOperationException exception)
		{
			ShowError(exception,true);
		}
		catch(Exception exception)
		{
			if(ReferenceEquals(scope,client))
			{
				await Disconnect();
			}
			ShowError(exception,true);
		}
		finally
		{
			commandPending=false;
			UpdateEnabled();
		}
	}

	private async Task CapturePreview()
	{
		if(busy || commandPending || scope is null)
		{
			return;
		}
		int[] channels=Channels;
		if(channels.Length == 0)
		{
			return;
		}
		busy=true;
		UpdateEnabled();
		ScopeClient client=scope;
		try
		{
			ScopeSnapshot? snapshot=null;
			bool completed=await operations.TryRunPreviewAsync(async()=>
			{
				snapshot=await Task.Run(()=>ReadSnapshot(client,channels));
			});
			if(completed &&
				snapshot is not null &&
				ReferenceEquals(scope,client))
			{
				ApplySnapshot(snapshot,false);
			}
		}
		catch(InvalidOperationException exception)
		{
			LiveCheckBox.IsChecked=false;
			ShowError(exception,false);
		}
		catch(Exception exception)
		{
			if(ReferenceEquals(scope,client))
			{
				await Disconnect();
			}
			ShowError(exception,false);
		}
		finally
		{
			busy=false;
			UpdateEnabled();
		}
	}

	private static ScopeSnapshot ReadSnapshot(
		ScopeClient client,
		int[] channels)
	{
		Waveform[] waves=client.Capture(channels);
		int[] capturedChannels=waves.Select(wave=>wave.Channel).ToArray();
		ChannelMeasurements[] measurements=
			client.Measurements(capturedChannels);
		AcquisitionState state=client.AcquisitionStatus();
		return new(waves,measurements,state);
	}

	private void ApplySnapshot(ScopeSnapshot snapshot,bool manual)
	{
		Plot.SetWaveforms(snapshot.Waves);
		lastMeasurements=snapshot.Measurements;
		UpdateMeasurementRows(lastMeasurements,false);
		UpdateAcquisition(snapshot.State);
		if(manual)
		{
			captured=snapshot.Waves;
		}
		DetailText.Text=
			string.Join(
				" | ",
				snapshot.Waves.Select(
					wave=>$"CH{wave.Channel}: {wave.Volts.Length:N0} pkt"))+
			" | "+DateTime.Now.ToString("HH:mm:ss");
		UpdateCursorButtons();
	}

	private void UpdateCursorButtons()
	{
		Button[] buttons=CursorButtons;
		for(int index=0;index < buttons.Length;index++)
		{
			Button button=buttons[index];
			bool active=Plot.IsCursorActive(index);
			bool selected=Plot.SelectedCursor == index;
			bool moving=Plot.MovingCursor == index;
			button.Content=$"Kursor {index+1}"+(moving ? " RUCH" : "");
			button.BorderThickness=selected ? new Thickness(3) : new Thickness(1);
			button.BorderBrush=selected ? Brushes.White : Brushes.Black;
			button.Background=active
				? CursorBrushes[index]
				: (Brush)FindResource("KoradInputBrush");
			button.Foreground=active ? Brushes.Black : Brushes.White;
		}
	}

	private void UpdateMeasurementRows(
		ChannelMeasurements[] measurements,
		bool stale)
	{
		Channel1Measurements.Text=MeasurementText(
			"CH1",
			measurements.SingleOrDefault(value=>value.Channel == 1),
			stale);
		Channel2Measurements.Text=MeasurementText(
			"CH2",
			measurements.SingleOrDefault(value=>value.Channel == 2),
			stale);
	}

	private static string MeasurementText(
		string channel,
		ChannelMeasurements? value,
		bool stale)
	{
		string prefix=channel+(stale && value is not null
			? " [nieaktualne]"
			: "");
		if(value is null)
		{
			return prefix+
				": Vpp -- | Vrms -- | Hz -- | Vmin -- | Vmax -- | Duty --";
		}
		return prefix+": Vpp "+FormatMeasurement(value.Vpp,"V")+
			" | Vrms "+FormatMeasurement(value.Vrms,"V")+
			" | Hz "+FormatMeasurement(value.Frequency,"Hz")+
			" | Vmin "+FormatMeasurement(value.Minimum,"V")+
			" | Vmax "+FormatMeasurement(value.Maximum,"V")+
			" | Duty "+(value.DutyPercent.HasValue
				? value.DutyPercent.Value.ToString(
					"0.##",
					System.Globalization.CultureInfo.InvariantCulture)+" %"
				: "--");
	}

	private static string FormatMeasurement(double? value,string unit)
	{
		return value.HasValue ? FormatEngineering(value.Value,unit) : "--";
	}

	private static string FormatEngineering(double value,string unit)
	{
		double absolute=Math.Abs(value);
		(double factor,string prefix)=absolute switch
		{
			>=1e6=>(1e6,"M"),
			>=1e3=>(1e3,"k"),
			>=1=>(1,""),
			>=1e-3=>(1e-3,"m"),
			>=1e-6=>(1e-6,"µ"),
			>0=>(1e-9,"n"),
			_=>(1,"")
		};
		return (value/factor).ToString(
			"0.###",
			System.Globalization.CultureInfo.InvariantCulture)+
			" "+prefix+unit;
	}

	private void UpdateAcquisition(AcquisitionState state)
	{
		AcquisitionText.Text="Stan oscyloskopu: "+state switch
		{
			AcquisitionState.Start=>"START",
			AcquisitionState.Stop=>"STOP",
			_=>scope is null ? "OFFLINE" : "NIEZNANY"
		};
		AcquisitionText.Foreground=state switch
		{
			AcquisitionState.Start=>Brushes.LimeGreen,
			AcquisitionState.Stop=>Brushes.Gold,
			_=>Brushes.Silver
		};
	}

	private async Task SaveCsv()
	{
		if(busy || commandPending || captured is null)
		{
			return;
		}
		SaveFileDialog dialog=new()
		{
			Filter="Przebieg CSV (*.csv)|*.csv",
			FileName=
				"SDS1102CML_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".csv"
		};
		busy=true;
		UpdateEnabled();
		Waveform[] data=captured;
		try
		{
			if(dialog.ShowDialog(Window.GetWindow(this)) != true)
			{
				return;
			}
			await Task.Run(()=>
			{
				string temp=
					dialog.FileName+"."+Guid.NewGuid().ToString("N")+".tmp";
				try
				{
					using(StreamWriter writer=
						new(temp,false,new UTF8Encoding(true)))
					{
						Csv.Write(writer,data);
					}
					File.Move(temp,dialog.FileName,true);
				}
				finally
				{
					if(File.Exists(temp))
					{
						File.Delete(temp);
					}
				}
			});
		}
		catch(Exception exception)
		{
			ShowError(exception,true);
		}
		finally
		{
			busy=false;
			UpdateEnabled();
		}
	}

	private void ShowError(Exception exception,bool showDialog)
	{
		if(showDialog)
		{
			MessageBox.Show(
				Window.GetWindow(this),
				exception.Message,
				AppInformation.ProductName,
				MessageBoxButton.OK,
				MessageBoxImage.Warning);
		}
	}

	private void LoadSettings()
	{
		try
		{
			if(File.Exists(SettingsPath))
			{
				Dictionary<string,string>? settings=
					JsonSerializer.Deserialize<Dictionary<string,string>>(
						File.ReadAllText(SettingsPath));
				AddressTextBox.Text=
					settings?.GetValueOrDefault("address") ?? "";
			}
		}
		catch(Exception)
		{
		}
	}

	private void SaveSettings()
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
			File.WriteAllText(
				SettingsPath,
				JsonSerializer.Serialize(
					new Dictionary<string,string>
					{
						{"address",AddressTextBox.Text.Trim()}
					}));
		}
		catch(Exception)
		{
		}
	}

	public async ValueTask DisposeAsync()
	{
		if(disposed)
		{
			return;
		}
		disposed=true;
		closing=true;
		timer.Stop();
		UpdateEnabled();
		while(busy || commandPending)
		{
			await Task.Delay(100);
		}
		await Disconnect();
		operations.Dispose();
	}
}
