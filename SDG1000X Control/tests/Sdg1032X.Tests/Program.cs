using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LabStation.Instruments.Discovery;
using LabStation.Instruments.Scpi;
using LabStation.Instruments.Scheduling;
using LabStation.Instruments.Transport;
using LabStation.UI;
using LabStation.UI.Controls;
using Sdg1032X.App;
using Sdg1032X.App.Controls;
using Sdg1032X.Core;

int failed=0;
int passed=0;

if(args.SequenceEqual(["--scan-hardware"]))
{
	using CancellationTokenSource timeout=new(TimeSpan.FromSeconds(45));
	IInstrumentNetworkScanner scanner=new InstrumentNetworkScanner();
	DiscoveredInstrument? discovered=scanner.FindFirstAsync(
		SiglentGeneratorClient.IsSupported,
		timeout.Token).GetAwaiter().GetResult();
	if(discovered is null)
	{
		Console.Error.WriteLine("Nie znaleziono obsługiwanego generatora SIGLENT SDG1032X.");
		return 1;
	}
	Console.WriteLine(
		$"Znaleziono {discovered.Identity.Manufacturer},"+
		$"{discovered.Identity.Model},"+
		$"{discovered.Identity.SerialNumber},"+
		$"{discovered.Identity.Firmware} pod adresem {discovered.Address}.");
	return 0;
}

void Test(string name,Action action)
{
	try
	{
		action();
		passed++;
		Console.WriteLine("PASS "+name);
	}
	catch(Exception exception)
	{
		failed++;
		Console.WriteLine("FAIL "+name+": "+exception);
	}
}

void Equal<T>(T expected,T actual)
{
	if(!EqualityComparer<T>.Default.Equals(expected,actual))
	{
		throw new Exception($"Oczekiwano {expected}, otrzymano {actual}");
	}
}

void Close(double expected,double actual,double tolerance=1e-9)
{
	if(Math.Abs(expected-actual)>tolerance)
	{
		throw new Exception($"Oczekiwano {expected}, otrzymano {actual}");
	}
}

void Reject(Action action)
{
	try
	{
		action();
	}
	catch(InvalidDataException)
	{
		return;
	}
	throw new Exception("Nieprawidłowe dane zostały zaakceptowane");
}

void RunSta(Action action)
{
	Exception? failure=null;
	Thread thread=new(()=>
	{
		try
		{
			action();
		}
		catch(Exception exception)
		{
			failure=exception;
		}
	});
	thread.SetApartmentState(ApartmentState.STA);
	thread.Start();
	thread.Join();
	if(failure is not null)
	{
		throw failure;
	}
}

void RunStaAsync(Func<Task> action)
{
	Exception? failure=null;
	Thread thread=new(()=>
	{
		Dispatcher dispatcher=Dispatcher.CurrentDispatcher;
		SynchronizationContext.SetSynchronizationContext(
			new DispatcherSynchronizationContext(dispatcher));
		DispatcherFrame frame=new();
		Task task=action();
		task.ContinueWith(completed=>
		{
			if(completed.IsFaulted)
			{
				failure=completed.Exception?.GetBaseException();
			}
			frame.Continue=false;
		},TaskScheduler.FromCurrentSynchronizationContext());
		Dispatcher.PushFrame(frame);
	});
	thread.SetApartmentState(ApartmentState.STA);
	thread.Start();
	thread.Join();
	if(failure is not null)
	{
		throw failure;
	}
}

IEnumerable<T> VisualChildren<T>(DependencyObject parent) where T : DependencyObject
{
	for(int index=0;index<VisualTreeHelper.GetChildrenCount(parent);index++)
	{
		DependencyObject child=VisualTreeHelper.GetChild(parent,index);
		if(child is T typed)
		{
			yield return typed;
		}
		foreach(T descendant in VisualChildren<T>(child))
		{
			yield return descendant;
		}
	}
}

IEnumerable<T> LogicalChildren<T>(DependencyObject parent) where T : DependencyObject
{
	foreach(object childValue in LogicalTreeHelper.GetChildren(parent))
	{
		if(childValue is not DependencyObject child)
		{
			continue;
		}
		if(child is T typed)
		{
			yield return typed;
		}
		foreach(T descendant in LogicalChildren<T>(child))
		{
			yield return descendant;
		}
	}
}

Test("Lista przebiegów obejmuje sześć podstawowych typów i własny",()=>
{
	BasicWaveform[] values=Enum.GetValues<BasicWaveform>();
	Equal(7,values.Length);
	Equal("SINE",SiglentProtocol.WaveformCode(BasicWaveform.Sine));
	Equal("SQUARE",SiglentProtocol.WaveformCode(BasicWaveform.Square));
	Equal("RAMP",SiglentProtocol.WaveformCode(BasicWaveform.Ramp));
	Equal("PULSE",SiglentProtocol.WaveformCode(BasicWaveform.Pulse));
	Equal("NOISE",SiglentProtocol.WaveformCode(BasicWaveform.Noise));
	Equal("DC",SiglentProtocol.WaveformCode(BasicWaveform.Dc));
	ChannelSnapshot arbitrary=SiglentProtocol.ParseSnapshot(
		1,
		"C1:BSWV WVTP,ARB,FRQ,1KHZ,AMP,2V,OFST,0V,PHSE,0",
		"C1:OUTP OFF,LOAD,HZ,PLRT,NOR");
	Equal(BasicWaveform.Arbitrary,arbitrary.Waveform);
});

Test("Plik własnego przebiegu wymaga par próbek i bezpiecznej nazwy",()=>
{
	ArbitraryWaveformData waveform=ArbitraryWaveformData.FromBytes(
		"wave 1.bin",
		[0x00,0xE0,0x00,0x20]);
	Equal("wave_1",waveform.Name);
	Equal(2,waveform.SampleCount);
	Equal(4,waveform.Data.Length);
	try
	{
		ArbitraryWaveformData.FromBytes("odd.bin",[0x00,0x01,0x02]);
	}
	catch(InvalidDataException)
	{
		return;
	}
	throw new Exception("Nieparzysta liczba bajtów przebiegu została zaakceptowana");
});

Test("Plik EasyWave CSV zachowuje parametry i koduje próbki SDG1000X",()=>
{
	string path=Path.Combine(
		Path.GetTempPath(),
		"LabStation-EasyWave-"+Guid.NewGuid().ToString("N")+".csv");
	try
	{
		File.WriteAllText(
			path,
			"data length,3\n"+
			"frequency,2500\n"+
			"amp,4\n"+
			"offset,1\n"+
			"phase,90\n"+
			",\n,\n,\n,\n,\n,\n,\n"+
			"xpos,value\n"+
			"0,-1\n"+
			"0.0001,1\n"+
			"0.0002,3\n");
		ArbitraryWaveformData waveform=ArbitraryWaveformData.FromFile(path);
		Equal(3,waveform.SampleCount);
		byte[] expected=[0x00,0xE0,0x00,0x00,0xFF,0x1F];
		if(!expected.SequenceEqual(waveform.Data))
		{
			throw new Exception("Próbki CSV nie zostały zakodowane jako 14-bit little-endian 2's complement");
		}
	}
	finally
	{
		File.Delete(path);
	}
});

Test("Plik EasyWave CSV odrzuca niezgodną deklarowaną liczbę próbek",()=>
{
	string path=Path.Combine(
		Path.GetTempPath(),
		"LabStation-EasyWave-invalid-"+Guid.NewGuid().ToString("N")+".csv");
	try
	{
		File.WriteAllText(
			path,
			"data length,3\nfrequency,1000\namp,2\noffset,0\nphase,0\n"+
			",\n,\n,\n,\n,\n,\n,\nxpos,value\n0,-1\n1,1\n");
		Reject(()=>ArbitraryWaveformData.FromFile(path));
	}
	finally
	{
		File.Delete(path);
	}
});

Test("Własny przebieg jest wysyłany binarnie i wybierany po nazwie",()=>
{
	using RecordingRawTransport transport=new();
	using SiglentGeneratorClient client=new(transport);
	ArbitraryWaveformData waveform=ArbitraryWaveformData.FromBytes(
		"wave1.bin",
		[0x00,0xE0,0x00,0x20]);
	client.UploadArbitraryWaveform(1,waveform,2000,4,0,0);
	byte[] header=Encoding.ASCII.GetBytes(
		"C1:WVDT WVNM,wave1,FREQ,2000,AMPL,4,OFST,0,PHASE,0,WAVEDATA,");
	byte[] expected=[..header,0x00,0xE0,0x00,0x20];
	if(!expected.SequenceEqual(transport.RawWrites.Single()))
	{
		throw new Exception("Nieprawidłowy transfer binarny WVDT");
	}
	Equal("C1:ARWV NAME,wave1",transport.Writes.Single());
});

Test("Polecenia SCPI używają kanału i formatu niezależnego od kultury",()=>
{
	CultureInfo.CurrentCulture=new("pl-PL");
	Equal("C1:BSWV WVTP,SINE",SiglentProtocol.WaveformCommand(1,BasicWaveform.Sine));
	Equal("C2:BSWV FRQ,1234.5",SiglentProtocol.ParameterCommand(2,GeneratorParameter.Frequency,1234.5));
	Equal("C1:OUTP LOAD,HZ",SiglentProtocol.LoadCommand(1,OutputLoad.HighImpedance));
	Equal("C2:OUTP PLRT,INVT",SiglentProtocol.PolarityCommand(2,OutputPolarity.Inverted));
	Equal("C1:OUTP OFF",SiglentProtocol.OutputCommand(1,false));
});

Test("Parser odczytuje podstawowy przebieg i stan wyjścia",()=>
{
	ChannelSnapshot value=SiglentProtocol.ParseSnapshot(
		1,
		"C1:BSWV WVTP,SQUARE,FRQ,1.25KHZ,AMP,3.3V,OFST,1.65V,PHSE,90,DUTY,40",
		"C1:OUTP ON,LOAD,HZ,PLRT,NOR");
	Equal(BasicWaveform.Square,value.Waveform);
	Close(1250,value.FrequencyHz);
	Close(3.3,value.AmplitudeVpp);
	Close(1.65,value.OffsetVolts);
	Close(90,value.PhaseDegrees);
	Close(40,value.DutyPercent);
	Equal(true,value.OutputEnabled);
	Equal(OutputLoad.HighImpedance,value.Load);
	Equal(OutputPolarity.Normal,value.Polarity);
});

Test("Parser odczytuje parametry szumu",()=>
{
	ChannelSnapshot value=SiglentProtocol.ParseSnapshot(
		2,
		"C2:BSWV WVTP,NOISE,STDEV,500MV,MEAN,-25MV",
		"C2:OUTP OFF,LOAD,50,PLRT,INVT");
	Equal(BasicWaveform.Noise,value.Waveform);
	Close(0.5,value.NoiseStandardDeviation);
	Close(-0.025,value.NoiseMean);
	Equal(false,value.OutputEnabled);
	Equal(OutputLoad.Ohms50,value.Load);
	Equal(OutputPolarity.Inverted,value.Polarity);
});

Test("Nieznany przebieg z urządzenia jest odrzucany",()=>
{
	Reject(()=>SiglentProtocol.ParseSnapshot(
		1,
		"C1:BSWV WVTP,CUSTOM",
		"C1:OUTP OFF,LOAD,HZ,PLRT,NOR"));
});

Test("Edytor wartości przyjmuje polski separator i skaluje jednostkę",()=>
{
	Close(0.0000025,EngineeringValue.ParseDisplay("2,5",0.000001));
	Equal("2,500",EngineeringValue.FormatDisplay(0.0000025,0.000001,3));
	Reject(()=>EngineeringValue.ParseDisplay("abc",1));
});

Test("Nagłówek kanału używa skróconych jednostek inżynierskich",()=>
{
	CultureInfo.CurrentCulture=new("pl-PL");
	Equal("3kHz",ChannelSummaryFormatter.FormatEngineering(3000,"Hz"));
	Equal("2,5mVpp",ChannelSummaryFormatter.FormatEngineering(0.0025,"Vpp"));
	Equal("CH1  ON\n3kHz  4Vpp",ChannelSummaryFormatter.Format(1,true,3000,4));
});

Test("Nagłówek kanału odrzuca nieprawidłowy numer i wartości",()=>
{
	try
	{
		ChannelSummaryFormatter.Format(3,false,1000,1);
	}
	catch(ArgumentOutOfRangeException)
	{
		try
		{
			ChannelSummaryFormatter.Format(1,false,double.NaN,1);
		}
		catch(ArgumentOutOfRangeException)
		{
			return;
		}
	}
	throw new Exception("Nieprawidłowe dane nagłówka zostały zaakceptowane");
});

Test("Kolejka zastępuje starszą wartość tego samego parametru",()=>
{
	LatestRequestQueue<string> queue=new();
	queue.EnqueueLatest("C1:FRQ","C1:BSWV FRQ,100");
	queue.EnqueueLatest("C1:FRQ","C1:BSWV FRQ,200");
	ScheduledRequest<string> request=queue.TakeNext();
	Equal("C1:BSWV FRQ,200",request.Value);
	Equal(0,queue.PendingCount);
});

Test("Polecenie wyjścia ma priorytet przed ustawieniami",()=>
{
	LatestRequestQueue<string> queue=new();
	queue.EnqueueLatest("C1:AMP","C1:BSWV AMP,2");
	queue.EnqueuePriority("C1:OUTP OFF");
	Equal("C1:OUTP OFF",queue.TakeNext().Value);
	Equal("C1:BSWV AMP,2",queue.TakeNext().Value);
});

Test("Włączenie wyjścia czeka na wcześniejsze nastawy",()=>
{
	LatestRequestQueue<string> queue=new();
	queue.EnqueueLatest("C1:AMP","C1:BSWV AMP,2");
	queue.EnqueueOrdered("C1:OUTP ON");
	Equal("C1:BSWV AMP,2",queue.TakeNext().Value);
	Equal("C1:OUTP ON",queue.TakeNext().Value);
});

Test("Anulowanie kolejki kończy wszystkie oczekujące zadania",()=>
{
	LatestRequestQueue<string> queue=new();
	ScheduledRequest<string> setting=queue.EnqueueLatest("C1:AMP","C1:BSWV AMP,2");
	ScheduledRequest<string> output=queue.EnqueueOrdered("C1:OUTP ON");
	ScheduledRequest<string> off=queue.EnqueuePriority("C2:OUTP OFF");
	queue.CancelAll();
	Equal(0,queue.PendingCount);
	if(!setting.Completion.IsCanceled || !output.Completion.IsCanceled || !off.Completion.IsCanceled)
	{
		throw new Exception("Nie wszystkie zadania zostały anulowane");
	}
});

Test("Zastąpione polecenie kończy oczekiwanie jako anulowane",()=>
{
	LatestRequestQueue<string> queue=new();
	ScheduledRequest<string> older=queue.EnqueueLatest("C1:FRQ","C1:BSWV FRQ,100");
	queue.EnqueueLatest("C1:FRQ","C1:BSWV FRQ,200");
	if(!older.Completion.IsCanceled)
	{
		throw new Exception("Starsze polecenie nadal oczekuje");
	}
});

Test("Klient akceptuje tylko generator SDG1032X",()=>
{
	using ScriptedTransport transport=new("SIGLENT,SDG1032X,123456,1.0");
	using SiglentGeneratorClient client=new(transport);
	Equal("SIGLENT,SDG1032X,123456,1.0",client.Initialize());
	using ScriptedTransport other=new("SIGLENT,SDG2042X,123456,1.0");
	using SiglentGeneratorClient rejected=new(other);
	Reject(()=>rejected.Initialize());
});

Test("Niestandardowe obciążenie nie jest przedstawiane jako 50 omów",()=>
{
	ChannelSnapshot value=SiglentProtocol.ParseSnapshot(
		1,
		"C1:BSWV WVTP,SINE,FRQ,1KHZ,AMP,1V,OFST,0V,PHSE,0",
		"C1:OUTP OFF,LOAD,75,PLRT,NOR");
	Equal(OutputLoad.Custom,value.Load);
	Close(75,value.LoadOhms ?? double.NaN);
	try
	{
		SiglentProtocol.LoadCommand(1,OutputLoad.Custom);
	}
	catch(ArgumentOutOfRangeException)
	{
		return;
	}
	throw new Exception("Próba zapisu niestandardowego obciążenia nie została odrzucona");
});

Test("Metadane aplikacji zachowują autora, wersję i licencję",()=>
{
	Equal("Mateusz Skipor",ProductInformation.AuthorName);
	Equal("Inżynier technik elektroniki",ProductInformation.AuthorProfession);
	Equal("mskiporsklep@op.pl",ProductInformation.AuthorEmail);
	Equal("0.2.4",ProductInformation.Version);
	Equal("Siglent SDG1000X Control v0.2.4",ProductInformation.GetWindowTitle());
	string license=ProductInformation.LoadLicenseText();
	if(!license.Contains("PolyForm Noncommercial License 1.0.0",StringComparison.Ordinal))
	{
		throw new Exception("Brak właściwego tekstu licencji");
	}
});

Test("Pozycja przebiegu udostępnia czytelną nazwę dla UI Automation",()=>
{
	WaveformChoice choice=new("Prostokąt",BasicWaveform.Square);
	Equal("Prostokąt",choice.ToString());
});

Test("Interfejs zachowuje kompaktowy rozmiar i pełne pola klikalne",()=>
{
	RunSta(()=>
	{
		Sdg1032X.App.App application=new();
		application.InitializeComponent();
		if(application.Resources.Contains("WindowBrush") ||
			application.Resources.Contains("ControlBrush") ||
			application.Resources.Contains("TextBrush") ||
			application.Resources.Contains("MutedTextBrush"))
		{
			throw new Exception("Generator nadal duplikuje zasoby wspólnego motywu");
		}
		MainWindow window=new();
		GeneratorView defaultView=LogicalChildren<GeneratorView>(window).Single();
		DockPanel root=(DockPanel)window.Content;
		root.Children.Remove(defaultView);
		GeneratorView generatorView=new(
			new StubScanner(null),
			_=>new GeneratorDeviceTransport(),
			new MemoryGeneratorSettingsStore(new("192.168.200.132",false)));
		root.Children.Add(generatorView);
		window.WindowStartupLocation=WindowStartupLocation.Manual;
		window.Left=-10000;
		window.Top=-10000;
		window.ShowActivated=false;
		window.Show();
		window.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
		window.Measure(new Size(350,749));
		window.Arrange(new Rect(0,0,350,749));
		window.ApplyTemplate();

		Equal(749d,window.Height);
		Directory.CreateDirectory(Path.Combine("SDG1000X Control","artifacts","qa"));
		RenderTargetBitmap bitmap=new(350,749,96,96,PixelFormats.Pbgra32);
		bitmap.Render(window);
		PngBitmapEncoder encoder=new();
		encoder.Frames.Add(BitmapFrame.Create(bitmap));
		using(FileStream stream=File.Create(Path.Combine(
			"SDG1000X Control",
			"artifacts",
			"qa",
			"generator-ui.png")))
		{
			encoder.Save(stream);
		}
		AboutMenuItem about=LogicalChildren<AboutMenuItem>(window).Single();
		Equal("O aplikacji",about.Header);
		if(about.Presentation?.DisplayName != ProductInformation.DisplayName)
		{
			throw new Exception("Menu informacji nie używa wspólnego modelu prezentacji");
		}
		SolidColorBrush background=(SolidColorBrush)window.FindResource("LabStationBackgroundBrush");
		Equal(Color.FromRgb(104,104,104),background.Color);
		TextBox host=LogicalChildren<TextBox>(window)
			.First(textBox=>textBox.Name == "HostEditor");
		Equal("192.168.200.132",host.Text);
		Equal(150d,host.ActualWidth);
		TextBlock ipLabel=LogicalChildren<TextBlock>(window)
			.Single(textBlock=>textBlock.Name == "IpLabel");
		Equal("IP:",ipLabel.Text);
		if(ipLabel.Foreground is not SolidColorBrush ipForeground ||
			ipForeground.Color != Colors.White)
		{
			throw new Exception("Etykieta IP nie ma białego tekstu");
		}
		if(host.Padding.Top>2 || host.Padding.Bottom>2)
		{
			throw new Exception("Pole IP ma zbyt duży pionowy margines wewnętrzny");
		}
		Button connection=LogicalChildren<Button>(window)
			.Single(button=>button.Name == "ConnectionButton");
		if(!double.IsNaN(connection.Width) || connection.MinWidth != 72)
		{
			throw new Exception("Przycisk połączenia nie ma wspólnego kompaktowego rozmiaru");
		}
		if(connection.Foreground is not SolidColorBrush connectionForeground ||
			connectionForeground.Color != Colors.White)
		{
			throw new Exception("Przycisk Offline nie ma białego tekstu");
		}
		Button output=LogicalChildren<Button>(window)
			.First(button=>button.Name == "OutputButton");
		Equal("OFF",output.Content);
		if(output.Foreground is not SolidColorBrush outputForeground ||
			outputForeground.Color != Colors.White)
		{
			throw new Exception("Przycisk OFF nie ma białego tekstu");
		}
		Color outputBackground=((SolidColorBrush)output.Background).Color;
		ChannelControl channel1=LogicalChildren<ChannelControl>(window)
			.Single(control=>control.Name == "Channel1");
		channel1.ApplySnapshot(new ChannelSnapshot
		{
			Channel=1,
			Waveform=BasicWaveform.Sine,
			OutputEnabled=true,
			Load=OutputLoad.HighImpedance,
			Polarity=OutputPolarity.Normal
		});
		Equal("ON",output.Content);
		Equal(outputBackground,((SolidColorBrush)output.Background).Color);
		if(output.Foreground is not SolidColorBrush outputOnForeground ||
			outputOnForeground.Color != Colors.White)
		{
			throw new Exception("Przycisk ON zmienił kolor tekstu");
		}
		TextBlock status=LogicalChildren<TextBlock>(window)
			.Single(textBlock=>textBlock.Name == "StatusText");
		Equal("Status: OFFLINE",status.Text);

		TabControl tabs=LogicalChildren<TabControl>(window)
			.Single(tabControl=>tabControl.Name == "ChannelTabs");
		TabItem[] tabItems=tabs.Items.OfType<TabItem>().ToArray();
		Equal(2,tabItems.Length);
		if(tabs.ActualWidth<300 || tabItems.Any(tabItem=>tabItem.ActualWidth<140))
		{
			throw new Exception(
				$"Nagłówki CH1/CH2 nie mają szerokości okna: {tabs.ActualWidth:G}; {tabItems[0].ActualWidth:G}, {tabItems[1].ActualWidth:G}");
		}
		if(Math.Abs(tabItems[0].ActualWidth-tabItems[1].ActualWidth)>1)
		{
			throw new Exception("Zakładki kanałów nie zajmują równych połówek");
		}
		if(tabItems.Any(tabItem=>tabItem.ActualWidth<tabs.ActualWidth*0.45))
		{
			throw new Exception("Zakładki kanałów nie wypełniają szerokości panelu");
		}
		if(tabItems.Any(tabItem=>tabItem.ActualHeight<24))
		{
			throw new Exception(
				$"Nagłówki CH1/CH2 nie są widoczne: {tabItems[0].ActualHeight:G}, {tabItems[1].ActualHeight:G}");
		}
		if(tabItems[0].Foreground is not SolidColorBrush activeForeground ||
			activeForeground.Color != Colors.Black)
		{
			throw new Exception("Aktywna zakładka nie ma czarnego tekstu");
		}
		if(tabItems[1].Foreground is not SolidColorBrush inactiveForeground ||
			inactiveForeground.Color != Colors.White)
		{
			throw new Exception("Nieaktywna zakładka nie ma białego tekstu");
		}
		if(tabItems[0].Background is SolidColorBrush activeBackground &&
			activeBackground.Color == Colors.White)
		{
			throw new Exception("Aktywna zakładka nadal ma białe tło");
		}

		if(LogicalChildren<TabControl>(channel1)
			.Any(tabControl=>tabControl.Name == "WaveformTabs"))
		{
			throw new Exception("Panel Przebieg nadal jest dodatkową zakładką");
		}
		TextBlock waveformLabel=LogicalChildren<TextBlock>(channel1)
			.Single(textBlock=>textBlock.Name == "WaveformLabel");
		Equal("Przebieg",waveformLabel.Text);
		if(waveformLabel.Foreground is not SolidColorBrush waveformLabelForeground ||
			waveformLabelForeground.Color != Colors.White)
		{
			throw new Exception("Etykieta Przebieg nie ma standardowego białego tekstu");
		}
		ComboBox waveformSelector=LogicalChildren<ComboBox>(channel1)
			.Single(comboBox=>comboBox.Name == "WaveformSelector");
		WaveformChoice arbitraryChoice=waveformSelector.Items
			.Cast<WaveformChoice>()
			.Single(choice=>choice.Value == BasicWaveform.Arbitrary);
		Equal("Arbitralne",arbitraryChoice.Name);
		if(waveformSelector.Items.Count != 7)
		{
			throw new Exception("Lista przebiegów nie zawiera wszystkich siedmiu pozycji");
		}
		FrameworkElement customPanel=LogicalChildren<FrameworkElement>(channel1)
			.Single(element=>element.Name == "CustomWaveformPanel");
		Equal(Visibility.Collapsed,customPanel.Visibility);
		TextBox customPath=LogicalChildren<TextBox>(channel1)
			.Single(textBox=>textBox.Name == "CustomWaveformPathEditor");
		Button browseCustom=LogicalChildren<Button>(channel1)
			.Single(button=>button.Name == "BrowseCustomWaveformButton");
		Button loadCustom=LogicalChildren<Button>(channel1)
			.Single(button=>button.Name == "LoadCustomWaveformButton");
		Equal("",customPath.Text);
		Equal("Wczytaj",loadCustom.Content);
		if(browseCustom.Content is not TextBlock folderIcon ||
			string.IsNullOrWhiteSpace(folderIcon.Text))
		{
			throw new Exception("Przycisk wyboru pliku nie ma ikony katalogu");
		}
		waveformSelector.SelectedItem=arbitraryChoice;
		window.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
		Equal(Visibility.Visible,customPanel.Visibility);
		foreach(string label in new[]{"Częstotliwość","Amplituda","Offset","Faza"})
		{
			TextBlock fieldLabel=LogicalChildren<TextBlock>(channel1)
				.Single(textBlock=>textBlock.Text == label);
			if(fieldLabel.Foreground is not SolidColorBrush fieldForeground ||
				fieldForeground.Color != Colors.White)
			{
				throw new Exception($"Etykieta {label} nie ma standardowego białego tekstu");
			}
		}
		RenderTargetBitmap customBitmap=new(350,749,96,96,PixelFormats.Pbgra32);
		customBitmap.Render(window);
		PngBitmapEncoder customEncoder=new();
		customEncoder.Frames.Add(BitmapFrame.Create(customBitmap));
		using(FileStream stream=File.Create(Path.Combine(
			"SDG1000X Control",
			"artifacts",
			"qa",
			"generator-custom-ui.png")))
		{
			customEncoder.Save(stream);
		}

		if(LogicalChildren<FrameworkElement>(window).Any(element=>
			element.GetType().FullName ==
			"Sdg1032X.App.Controls.NumericEditor"))
		{
			throw new Exception("Generator nadal używa lokalnego duplikatu edytora liczbowego");
		}
		if(!LogicalChildren<NumericValueEditor>(window).Any())
		{
			throw new Exception("Generator nie używa wspólnego edytora wartości");
		}
		FrameworkElement stepSelector=LogicalChildren<FrameworkElement>(channel1)
			.Single(element=>element.Name == "StepMultiplierSelector");
		Button[] stepButtons=LogicalChildren<Button>(stepSelector)
			.Where(button=>button.Tag is double)
			.ToArray();
		string[] stepButtonNames=stepButtons
			.Select(button=>button.Content?.ToString() ?? "")
			.ToArray();
		if(!stepButtonNames.SequenceEqual(["G","M","k","1","m","u","n"]))
		{
			throw new Exception("Selektor kroku nie zawiera kompletu mnożników SI");
		}
		Button activeStep=stepButtons.Single(button=>Equals(button.Content,"m"));
		if(activeStep.Background is not SolidColorBrush activeStepBackground ||
			activeStepBackground.Color != Color.FromRgb(0,255,0) ||
			activeStep.Foreground is not SolidColorBrush activeStepForeground ||
			activeStepForeground.Color != Colors.Black)
		{
			throw new Exception("Aktywny mnożnik nie ma zielonego tła i czarnego tekstu");
		}
		Button inactiveStep=stepButtons.Single(button=>Equals(button.Content,"1"));
		if(inactiveStep.Background is SolidColorBrush inactiveStepBackground &&
			inactiveStepBackground.Color == Color.FromRgb(0,255,0) ||
			inactiveStep.Foreground is not SolidColorBrush inactiveStepForeground ||
			inactiveStepForeground.Color != Colors.White)
		{
			throw new Exception("Nieaktywny mnożnik nie zachowuje standardowego wyglądu przycisku");
		}
		NumericValueEditor frequencyEditor=LogicalChildren<NumericValueEditor>(channel1)
			.Single(editor=>editor.Name == "FrequencyEditor");
		NumericValueEditor amplitudeEditor=LogicalChildren<NumericValueEditor>(channel1)
			.Single(editor=>editor.Name == "AmplitudeEditor");
		NumericValueEditor phaseEditor=LogicalChildren<NumericValueEditor>(channel1)
			.Single(editor=>editor.Name == "PhaseEditor");
		NumericValueEditor dutyEditor=LogicalChildren<NumericValueEditor>(channel1)
			.Single(editor=>editor.Name == "DutyEditor");
		NumericValueEditor pulseWidthEditor=LogicalChildren<NumericValueEditor>(channel1)
			.Single(editor=>editor.Name == "PulseWidthEditor");
		Equal(0.001,frequencyEditor.Step);
		Equal(0.001,amplitudeEditor.Step);
		Equal(0.1,phaseEditor.Step);
		Equal(0.1,dutyEditor.Step);
		Equal(0.000000001,pulseWidthEditor.Step);
		inactiveStep.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
		Equal(1d,frequencyEditor.Step);
		Equal(1d,amplitudeEditor.Step);
		Equal(1d,phaseEditor.Step);
		Equal(0.000001,pulseWidthEditor.Step);
		Button kiloStep=stepButtons.Single(button=>Equals(button.Content,"k"));
		kiloStep.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
		dutyEditor.Value=0;
		RepeatButton dutyIncrement=LogicalChildren<RepeatButton>(dutyEditor)
			.Single(button=>Equals(button.Content,"▲"));
		RepeatButton dutyDecrement=LogicalChildren<RepeatButton>(dutyEditor)
			.Single(button=>Equals(button.Content,"▼"));
		dutyIncrement.RaiseEvent(new RoutedEventArgs(RepeatButton.ClickEvent));
		Equal(100d,dutyEditor.Value);
		dutyDecrement.RaiseEvent(new RoutedEventArgs(RepeatButton.ClickEvent));
		Equal(0d,dutyEditor.Value);
		Button microStep=stepButtons.Single(button=>Equals(button.Content,"u"));
		microStep.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
		Equal(0.001,frequencyEditor.Step);
		Equal(0.1,dutyEditor.Step);
		MenuItem tools=LogicalChildren<MenuItem>(window)
			.Single(item=>Equals(item.Header,"Narzędzia"));
		string[] toolItems=LogicalChildren<MenuItem>(tools)
			.Select(item=>item.Header?.ToString() ?? "")
			.ToArray();
		if(!toolItems.Contains("Skanuj sieć") || !toolItems.Contains("Auto connect"))
		{
			throw new Exception("Brak wspólnych funkcji sieciowych w menu Narzędzia");
		}
		if(LogicalChildren<Button>(window)
			.Any(button=>Equals(button.Content,"Odczytaj kanał")))
		{
			throw new Exception("Pozostał zbędny przycisk odczytu kanału");
		}

		ComboBox combo=LogicalChildren<ComboBox>(window).First();
		combo.Measure(new Size(300,30));
		combo.Arrange(new Rect(0,0,300,30));
		combo.ApplyTemplate();
		ToggleButton toggle=VisualChildren<ToggleButton>(combo).Single();
		if(toggle.ActualWidth+1<combo.ActualWidth)
		{
			throw new Exception("Lista otwiera się tylko po kliknięciu strzałki");
		}

		using ScriptedTransport transport=new("SIGLENT,SDG1032X,123456,1.0");
		GeneratorSession closingSession=GeneratorSession.CreateAsync(()=>transport)
			.GetAwaiter()
			.GetResult();
		System.Reflection.FieldInfo sessionField=typeof(GeneratorView).GetField(
			"session",
			System.Reflection.BindingFlags.Instance |
			System.Reflection.BindingFlags.NonPublic)!;
		System.Reflection.MethodInfo failureMethod=typeof(GeneratorView).GetMethod(
			"SessionCommunicationFailed",
			System.Reflection.BindingFlags.Instance |
			System.Reflection.BindingFlags.NonPublic)!;
		EventHandler<Exception> failureHandler=(EventHandler<Exception>)
			Delegate.CreateDelegate(typeof(EventHandler<Exception>),generatorView,failureMethod);
		sessionField.SetValue(generatorView,closingSession);
		closingSession.CommunicationFailed+=failureHandler;
		System.Reflection.MethodInfo unloadedMethod=typeof(GeneratorView).GetMethod(
			"GeneratorViewUnloaded",
			System.Reflection.BindingFlags.Instance |
			System.Reflection.BindingFlags.NonPublic)!;
		unloadedMethod.Invoke(generatorView,[null,EventArgs.Empty]);
		System.Reflection.FieldInfo eventField=typeof(GeneratorSession).GetField(
			"CommunicationFailed",
			System.Reflection.BindingFlags.Instance |
			System.Reflection.BindingFlags.NonPublic)!;
		if(eventField.GetValue(closingSession) is not null)
		{
			throw new Exception("Panel pozostawia obsługę błędu podłączoną podczas zamykania");
		}
		generatorView.DisposeAsync().AsTask().GetAwaiter().GetResult();
		window.Hide();
	});
});

Test("Ustawienia Generatora zachowują adres i Auto connect",()=>
{
	string directory=Path.Combine(
		Path.GetTempPath(),
		"LabStation-GeneratorSettings-"+Guid.NewGuid().ToString("N"));
	string path=Path.Combine(directory,"settings.json");
	try
	{
		JsonGeneratorSettingsStore store=new(
			path,
			new("192.168.200.132",false));
		Equal(
			new GeneratorSettings("192.168.200.132",false),
			store.Load());
		store.Save(new("192.168.200.50",true));
		Equal(
			new GeneratorSettings("192.168.200.50",true),
			store.Load());
	}
	finally
	{
		if(Directory.Exists(directory))
		{
			Directory.Delete(directory,true);
		}
	}
});

Test("Skan wpisuje adres Generatora i Auto connect nawiązuje połączenie",()=>
{
	RunStaAsync(async()=>
	{
		MemoryGeneratorSettingsStore settings=new(
			new("",true));
		StubScanner scanner=new(
			new(
				"192.168.200.132",
				new("SIGLENT","SDG1032X","123456","1.0")));
		GeneratorView view=new(
			scanner,
			_=>new GeneratorDeviceTransport(),
			settings);
		Button connection=LogicalChildren<Button>(view)
			.Single(button=>button.Name == "ConnectionButton");
		Color disconnectedBackground=((SolidColorBrush)connection.Background).Color;
		await view.ScanNetworkAsync();
		TextBox host=LogicalChildren<TextBox>(view)
			.Single(textBox=>textBox.Name == "HostEditor");
		Equal("192.168.200.132",host.Text);
		Equal("Online",connection.Content);
		Equal(disconnectedBackground,((SolidColorBrush)connection.Background).Color);
		if(connection.Foreground is not SolidColorBrush onlineForeground ||
			onlineForeground.Color != Colors.White)
		{
			throw new Exception("Przycisk Online zmienił kolor tekstu");
		}
		Equal(false,host.IsEnabled);
		Equal(
			new GeneratorSettings("192.168.200.132",true),
			settings.Value);
		await view.DisposeAsync();
	});
});

Console.WriteLine($"Wynik: {passed} zaliczonych, {failed} niezaliczonych");
return failed == 0 ? 0 : 1;

sealed class ScriptedTransport(string identity) : IInstrumentTransport
{
	public void Write(string command)
	{
	}

	public byte[] Query(string command)
	{
		return Encoding.ASCII.GetBytes(identity);
	}

	public void Dispose()
	{
	}
}

sealed class RecordingRawTransport : IInstrumentTransport,IRawInstrumentTransport
{
	public List<string> Writes { get; }=[];
	public List<byte[]> RawWrites { get; }=[];

	public void Write(string command)
	{
		Writes.Add(command);
	}

	public void Write(ReadOnlyMemory<byte> data)
	{
		RawWrites.Add(data.ToArray());
	}

	public byte[] Query(string command)
	{
		return Encoding.ASCII.GetBytes("SIGLENT,SDG1032X,123456,1.0");
	}

	public void Dispose()
	{
	}
}

sealed class StubScanner(DiscoveredInstrument? result) : IInstrumentNetworkScanner
{
	public Task<DiscoveredInstrument?> FindFirstAsync(
		Func<ScpiIdentity,bool> isSupported,
		CancellationToken cancellationToken)
	{
		return Task.FromResult(
			result is not null && isSupported(result.Identity)
				? result
				: null);
	}
}

sealed class MemoryGeneratorSettingsStore(GeneratorSettings value) : IGeneratorSettingsStore
{
	public GeneratorSettings Value { get; private set; }=value;

	public GeneratorSettings Load()
	{
		return Value;
	}

	public void Save(GeneratorSettings settings)
	{
		Value=settings;
	}
}

sealed class GeneratorDeviceTransport : IInstrumentTransport
{
	public void Write(string command)
	{
	}

	public byte[] Query(string command)
	{
		string response=command switch
		{
			"*IDN?"=>"SIGLENT,SDG1032X,123456,1.0",
			"C1:BSWV?"=>"C1:BSWV WVTP,SINE,FRQ,3000HZ,AMP,4V,OFST,0V,PHSE,0",
			"C2:BSWV?"=>"C2:BSWV WVTP,SINE,FRQ,2000HZ,AMP,3V,OFST,0V,PHSE,0",
			"C1:OUTP?"=>"C1:OUTP OFF,LOAD,HZ,PLRT,NOR",
			"C2:OUTP?"=>"C2:OUTP OFF,LOAD,HZ,PLRT,NOR",
			_=>throw new InvalidOperationException(command)
		};
		return Encoding.ASCII.GetBytes(response);
	}

	public void Dispose()
	{
	}
}
