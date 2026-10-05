using System.IO;
using System.Text;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LabStation.Instruments.Discovery;
using LabStation.Instruments.Scpi;
using LabStation.Instruments.Transport;
using Sdm3000.App;
using Sdm3000.Core;

int failed=0;
int passed=0;

if(args.Length > 0 && args[0] == "--hardware-read")
{
	string address=args.Length > 1 ? args[1] : "192.168.200.131";
	MultimeterSession hardware=MultimeterSession.ConnectAsync(address)
		.GetAwaiter()
		.GetResult();
	try
	{
		Console.WriteLine(
			"IDN="+hardware.Identity.Manufacturer+","+
			hardware.Identity.Model+","+
			hardware.Identity.SerialNumber+","+
			hardware.Identity.Firmware);
		for(int index=0;index<3;index++)
		{
			MeasurementSnapshot snapshot=hardware.ReadAsync()
				.GetAwaiter()
				.GetResult();
			MeasurementProfile profile=MeasurementProfiles.For(snapshot.Configuration.Function);
			Console.WriteLine(
				"READ="+profile.ShortName+" "+
				MeasurementFormatter.FormatValue(snapshot.Reading,profile.Unit)+
				" RAW="+(snapshot.Reading.Value?.ToString("G17") ?? snapshot.Reading.State.ToString())+
				" RANGE="+MeasurementFormatter.FormatRange(snapshot.Configuration.Range,profile.Unit)+
				" POINTS="+snapshot.StoredPoints);
			Thread.Sleep(500);
		}
	}
	finally
	{
		hardware.DisposeAsync().AsTask().GetAwaiter().GetResult();
	}
	return 0;
}

if(args.SequenceEqual(["--scan-hardware"]))
{
	using CancellationTokenSource timeout=new(TimeSpan.FromSeconds(45));
	IInstrumentNetworkScanner scanner=new InstrumentNetworkScanner();
	DiscoveredInstrument? discovered=scanner.FindFirstAsync(
		SiglentMultimeterClient.IsSupported,
		timeout.Token).GetAwaiter().GetResult();
	if(discovered is null)
	{
		Console.Error.WriteLine("Nie znaleziono obsługiwanego multimetru SIGLENT SDM3000.");
		return 1;
	}
	Console.WriteLine(
		"Znaleziono "+discovered.Identity.Manufacturer+","+
		discovered.Identity.Model+","+
		discovered.Identity.SerialNumber+","+
		discovered.Identity.Firmware+" pod adresem "+discovered.Address+".");
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

void EqualStrings(string[] expected,string[] actual)
{
	if(!expected.SequenceEqual(actual))
	{
		throw new Exception(
			"Oczekiwano "+string.Join(",",expected)+
			", otrzymano "+string.Join(",",actual));
	}
}

void Close(double expected,double actual,double tolerance=1e-9)
{
	if(Math.Abs(expected-actual) > tolerance)
	{
		throw new Exception($"Oczekiwano {expected}, otrzymano {actual}");
	}
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

IEnumerable<T> LogicalChildren<T>(DependencyObject root) where T : DependencyObject
{
	foreach(object child in LogicalTreeHelper.GetChildren(root))
	{
		if(child is not DependencyObject dependency)
		{
			continue;
		}
		if(dependency is T typed)
		{
			yield return typed;
		}
		foreach(T nested in LogicalChildren<T>(dependency))
		{
			yield return nested;
		}
	}
}

Test("Rozpoznaje funkcje pomiarowe zwracane przez SDM3055",()=>
{
	(string Response,MeasurementFunction Function,double? Range)[] cases=
	[
		("\"VOLT:DC\",2.000000E+02",MeasurementFunction.VoltageDc,200),
		("VOLT:AC 2.000000E+02",MeasurementFunction.VoltageAc,200),
		("CURR:DC,2.000000E+00",MeasurementFunction.CurrentDc,2),
		("CURR:AC 2.000000E+00",MeasurementFunction.CurrentAc,2),
		("RES 2.000000E+03",MeasurementFunction.Resistance2Wire,2000),
		("FRES,2.000000E+03",MeasurementFunction.Resistance4Wire,2000),
		("CAP 2.000000E-06",MeasurementFunction.Capacitance,0.000002),
		("CONT",MeasurementFunction.Continuity,null),
		("DIOD",MeasurementFunction.Diode,null),
		("FREQ",MeasurementFunction.Frequency,null),
		("PER",MeasurementFunction.Period,null),
		("TEMP",MeasurementFunction.Temperature,null)
	];
	foreach((string response,MeasurementFunction function,double? range) in cases)
	{
		MeasurementConfiguration parsed=MultimeterProtocol.ParseConfiguration(response);
		Equal(function,parsed.Function);
		Equal(range,parsed.Range);
	}
});

Test("Buduje polecenia konfiguracji ośmiu funkcji dostępnych w aplikacji",()=>
{
	Equal("CONFigure:VOLTage:AC",MultimeterProtocol.ConfigureCommand(MeasurementFunction.VoltageAc));
	Equal("CONFigure:VOLTage:DC",MultimeterProtocol.ConfigureCommand(MeasurementFunction.VoltageDc));
	Equal("CONFigure:CURRent:AC",MultimeterProtocol.ConfigureCommand(MeasurementFunction.CurrentAc));
	Equal("CONFigure:CURRent:DC",MultimeterProtocol.ConfigureCommand(MeasurementFunction.CurrentDc));
	Equal("CONFigure:RESistance",MultimeterProtocol.ConfigureCommand(MeasurementFunction.Resistance2Wire));
	Equal("CONFigure:CAPacitance",MultimeterProtocol.ConfigureCommand(MeasurementFunction.Capacitance));
	Equal("CONFigure:DIODe",MultimeterProtocol.ConfigureCommand(MeasurementFunction.Diode));
	Equal("CONFigure:CONTinuity",MultimeterProtocol.ConfigureCommand(MeasurementFunction.Continuity));
});

Test("Dobiera główną nazwę i jednostkę bez wyboru trybu w UI",()=>
{
	Equal(new MeasurementProfile("Napięcie DC","V DC","Vdc","V"),
		MeasurementProfiles.For(MeasurementFunction.VoltageDc));
	Equal(new MeasurementProfile("Napięcie AC","V AC","Vrms","V"),
		MeasurementProfiles.For(MeasurementFunction.VoltageAc));
	Equal(new MeasurementProfile("Prąd DC","A DC","Idc","A"),
		MeasurementProfiles.For(MeasurementFunction.CurrentDc));
	Equal(new MeasurementProfile("Prąd AC","A AC","Irms","A"),
		MeasurementProfiles.For(MeasurementFunction.CurrentAc));
	Equal("Ω",MeasurementProfiles.For(MeasurementFunction.Resistance2Wire).Unit);
	Equal("F",MeasurementProfiles.For(MeasurementFunction.Capacitance).Unit);
	Equal("Vf",MeasurementProfiles.For(MeasurementFunction.Diode).PrimaryLabel);
	Equal("Ciągłość",MeasurementProfiles.For(MeasurementFunction.Continuity).Name);
});

Test("Rozpoznaje przeciążenie zamiast pokazywać 9,9E37",()=>
{
	MeasurementReading normal=MultimeterProtocol.ParseReading("-1.234500E-03");
	Equal(ReadingState.Value,normal.State);
	Close(-0.0012345,normal.Value!.Value);
	Equal(ReadingState.Overload,MultimeterProtocol.ParseReading("9.900000E+37").State);
	Equal(ReadingState.Overload,MultimeterProtocol.ParseReading("+9.91E37").State);
});

Test("Wylicza lokalne statystyki serii i ignoruje przeciążenie",()=>
{
	LocalStatistics statistics=new();
	statistics.Add(MultimeterProtocol.ParseReading("1"));
	statistics.Add(MultimeterProtocol.ParseReading("2"));
	statistics.Add(MultimeterProtocol.ParseReading("4"));
	statistics.Add(MultimeterProtocol.ParseReading("9.9E37"));
	LocalStatisticsSnapshot result=statistics.Snapshot();
	Equal(3L,result.Count);
	Close(1,result.Minimum!.Value);
	Close(4,result.Maximum!.Value);
	Close(7d/3,result.Average!.Value);
	Close(3,result.PeakToPeak!.Value);
	Close(Math.Sqrt(14d/9),result.StandardDeviation!.Value);
});

Test("Zmiana funkcji rozpoczyna osobną serię statystyk",()=>
{
	MeasurementAccumulator accumulator=new();
	MeasurementSnapshot first=accumulator.Accept(
		new(MeasurementFunction.VoltageDc,10),
		MultimeterProtocol.ParseReading("2"),
		5);
	MeasurementSnapshot second=accumulator.Accept(
		new(MeasurementFunction.VoltageDc,10),
		MultimeterProtocol.ParseReading("4"),
		6);
	MeasurementSnapshot changed=accumulator.Accept(
		new(MeasurementFunction.Resistance2Wire,2000),
		MultimeterProtocol.ParseReading("100"),
		7);
	Equal(1L,first.Statistics.Count);
	Equal(2L,second.Statistics.Count);
	Equal(1L,changed.Statistics.Count);
	Close(100,changed.Statistics.Average!.Value);
});

Test("Klient inicjuje świeży pomiar poleceniem READ",()=>
{
	using RecordingTransport transport=new();
	using SiglentMultimeterClient client=new(transport);
	ScpiIdentity identity=client.Initialize();
	MeasurementSnapshot snapshot=client.ReadSnapshot(new MeasurementAccumulator());
	Equal("SDM3055",identity.Model);
	Equal(MeasurementFunction.VoltageAc,snapshot.Configuration.Function);
	Close(1.2345,snapshot.Reading.Value!.Value);
	Equal(17L,snapshot.StoredPoints);
	EqualStrings(
		["*IDN?","CONFigure?","READ?","DATA:POINts?"],
		transport.Queries.ToArray());
	Equal(0,transport.LocalRequests);
	if(transport.Writes.Count != 0)
	{
		throw new Exception("Klient wysłał polecenie zmieniające stan miernika");
	}
});

Test("Klient utrzymuje sesję zdalną także po błędzie odczytu",()=>
{
	using RecordingTransport transport=new();
	using SiglentMultimeterClient client=new(transport);
	client.Initialize();
	transport.FailOnQuery="READ?";
	try
	{
		client.ReadSnapshot(new MeasurementAccumulator());
		throw new Exception("Oczekiwano błędu odczytu");
	}
	catch(IOException)
	{
	}
	Equal(0,transport.LocalRequests);
});

Test("Eksport CSV zachowuje czas, funkcję, wartość i przeciążenie",()=>
{
	MeasurementSnapshot[] snapshots=
	[
		new(
			new(MeasurementFunction.VoltageDc,0.2),
			new(ReadingState.Value,0.01149789),
			43,
			new(1,0.01149789,0.01149789,0.01149789,0,0),
			new DateTimeOffset(2026,10,3,12,34,56,TimeSpan.FromHours(2))),
		new(
			new(MeasurementFunction.Diode,null),
			new(ReadingState.Overload,null),
			44,
			new(1,null,null,null,null,null),
			new DateTimeOffset(2026,10,3,12,34,57,TimeSpan.FromHours(2)))
	];
	using StringWriter writer=new();
	MeasurementCsvWriter.Write(writer,snapshots);
	string csv=writer.ToString();
	if(!csv.Contains("Czas;Funkcja;Etykieta;Wartosc;Jednostka;Zakres;Stan;PunktyPamieci") ||
		!csv.Contains("2026-10-03T12:34:56.0000000+02:00;VoltageDc;Vdc;0.01149789;V;0.2;VALUE;43") ||
		!csv.Contains("Diode;Vf;;V;;OVERLOAD;44"))
	{
		throw new Exception("Eksport CSV nie zawiera oczekiwanych danych");
	}
});

Test("Profil sprzętu akceptuje rodzinę SDM3000 firmy SIGLENT",()=>
{
	Equal(true,SiglentMultimeterClient.IsSupported(
		new("SIGLENT TECHNOLOGIES","SDM3055","A","1.0")));
	Equal(true,SiglentMultimeterClient.IsSupported(
		new("SIGLENT","SDM3065X","A","1.0")));
	Equal(false,SiglentMultimeterClient.IsSupported(
		new("SIGLENT","SDG1032X","A","1.0")));
});

Test("Formatuje wynik miernika z prefiksem inżynierskim",()=>
{
	Equal("1,23450 mV",MeasurementFormatter.FormatValue(
		new(ReadingState.Value,0.0012345),"V"));
	Equal("1,23400 kΩ",MeasurementFormatter.FormatValue(
		new(ReadingState.Value,1234),"Ω"));
	Equal("OL",MeasurementFormatter.FormatValue(
		new(ReadingState.Overload,null),"V"));
	Equal("200 V",MeasurementFormatter.FormatRange(200,"V"));
	Equal("-",MeasurementFormatter.FormatRange(null,"V"));
});

Test("Sesja zachowuje jeden transport dla konfiguracji i kolejnych odczytów",()=>
{
	List<RecordingTransport> transports=[];
	MultimeterSession session=MultimeterSession.CreateAsync(()=>
	{
		RecordingTransport transport=new();
		transports.Add(transport);
		return transport;
	})
		.GetAwaiter()
		.GetResult();
	try
	{
		Equal("SDM3055",session.Identity.Model);
		Equal(1,transports.Count);
		Equal(false,transports[0].Disposed);
		session.ConfigureAsync(MeasurementFunction.CurrentDc)
			.GetAwaiter()
			.GetResult();
		EqualStrings(["CONFigure:CURRent:DC"],transports[0].Writes.ToArray());
		MeasurementSnapshot snapshot=session.ReadAsync()
			.GetAwaiter()
			.GetResult();
		Equal(MeasurementFunction.VoltageAc,snapshot.Configuration.Function);
		Equal(1L,snapshot.Statistics.Count);
		Equal(1,transports.Count);
		Equal(false,transports[0].Disposed);
	}
	finally
	{
		session.DisposeAsync().AsTask().GetAwaiter().GetResult();
	}
	Equal(true,transports[0].Disposed);
	Equal(1,transports[0].LocalRequests);
});

Test("Okno SDM jest poziome, stałe i udostępnia osiem funkcji pomiarowych",()=>
{
	RunSta(()=>
	{
		Application application=new();
		application.Resources.MergedDictionaries.Add(new ResourceDictionary
		{
			Source=new Uri(
				"pack://application:,,,/LabStation.UI;component/Themes/LabStationTheme.xaml")
		});
		MainWindow window=new();
		window.WindowStartupLocation=WindowStartupLocation.Manual;
		window.Left=-10000;
		window.Top=-10000;
		window.ShowActivated=false;
		window.Show();
		window.Dispatcher.Invoke(()=>{});

		Equal("0.2.4",AppInformation.Version);
		Equal("Siglent SDM3000 Control v0.2.4",AppInformation.DisplayName);
		Equal("Siglent SDM3000 Control v0.2.4",window.Title);
		Equal("Siglent.SDM3000.Control",typeof(MainWindow).Assembly.GetName().Name);
		if(window.Width>540d)
		{
			throw new Exception("Okno multimetru jest nadal zbyt szerokie");
		}
		if(window.Height<335d || window.Height>350d)
		{
			throw new Exception("Wysokość okna nie zapewnia miejsca na pełny główny odczyt");
		}
		Equal(ResizeMode.CanMinimize,window.ResizeMode);
		if(window.Width <= window.Height*1.4)
		{
			throw new Exception("Okno nie ma poziomej orientacji");
		}
		TextBox address=LogicalChildren<TextBox>(window)
			.Single(textBox=>textBox.Name == "HostEditor");
		Equal("192.168.200.131",address.Text);
		Equal(150d,address.Width);
		UniformGrid functionGrid=LogicalChildren<UniformGrid>(window)
			.Single(grid=>grid.Name == "FunctionButtons");
		Equal(4,functionGrid.Columns);
		Equal(2,functionGrid.Rows);
		if(double.IsNaN(functionGrid.Width) || functionGrid.Width>500d)
		{
			throw new Exception("Przyciski funkcji nie mają kompaktowej, standardowej szerokości");
		}
		string[] functions=["VoltageAc","VoltageDc","CurrentAc","CurrentDc","Resistance2Wire","Capacitance","Diode","Continuity"];
		Button[] functionButtons=LogicalChildren<Button>(functionGrid)
			.Where(button=>button.Tag is not null)
			.ToArray();
		string[] actualFunctions=functionButtons
			.Select(button=>button.Tag?.ToString() ?? "")
			.ToArray();
		if(!functions.SequenceEqual(actualFunctions))
		{
			throw new Exception("Interfejs nie zawiera pełnego wyboru funkcji pomiarowych");
		}
		foreach(string buttonName in new[]{"CapacitanceButton","DiodeButton","ContinuityButton"})
		{
			Button symbolButton=functionButtons.Single(button=>button.Name == buttonName);
			if(symbolButton.Content is not Viewbox ||
				string.IsNullOrWhiteSpace(symbolButton.ToolTip?.ToString()))
			{
				throw new Exception("Przycisk "+buttonName+" nie używa wektorowego symbolu z opisem po najechaniu");
			}
		}
		Button resistance=functionButtons.Single(button=>button.Name == "ResistanceButton");
		if(resistance.Content is not TextBlock resistanceSymbol || resistanceSymbol.FontSize<18d)
		{
			throw new Exception("Symbol omomierza jest mniejszy od tekstu pozostałych funkcji");
		}
		Button continuity=functionButtons.Single(button=>button.Name == "ContinuityButton");
		int continuityDots=LogicalChildren<System.Windows.Shapes.Ellipse>(continuity).Count();
		int continuityArcs=LogicalChildren<System.Windows.Shapes.Path>(continuity).Count();
		if(continuityDots != 1 || continuityArcs != 2)
		{
			throw new Exception("Symbol ciągłości nie składa się z kropki i dwóch osobnych łuków");
		}
		if(LogicalChildren<TextBlock>(window).Any(text=>text.Name == "PrimaryLabelText"))
		{
			throw new Exception("Główny odczyt nadal ma zbędny dopisek typu Vdc, Vrms lub Irms");
		}
		string[] statistics=LogicalChildren<TextBlock>(window)
			.Where(text=>text.Name.StartsWith("Statistic",StringComparison.Ordinal))
			.Select(text=>text.Text)
			.ToArray();
		EqualStrings(["Min","Max","Średnia"],statistics);
		MultimeterView multimeter=LogicalChildren<MultimeterView>(window).Single();
		MethodInfo applySnapshot=typeof(MultimeterView).GetMethod(
			"ApplySnapshot",
			BindingFlags.Instance|BindingFlags.NonPublic)
			?? throw new Exception("Brak obsługi prezentacji wyniku Multimetru");
		(TextBlock ShortText,Viewbox Symbol,MeasurementFunction Function)[] functionSymbols=
		[
			(LogicalChildren<TextBlock>(window).Single(text=>text.Name == "FunctionShortText"),
				LogicalChildren<Viewbox>(window).SingleOrDefault(view=>view.Name == "CapacitanceFunctionSymbol")
					?? throw new Exception("Pole pomiaru nie ma symbolu pojemności"),
				MeasurementFunction.Capacitance),
			(LogicalChildren<TextBlock>(window).Single(text=>text.Name == "FunctionShortText"),
				LogicalChildren<Viewbox>(window).SingleOrDefault(view=>view.Name == "DiodeFunctionSymbol")
					?? throw new Exception("Pole pomiaru nie ma symbolu diody"),
				MeasurementFunction.Diode),
			(LogicalChildren<TextBlock>(window).Single(text=>text.Name == "FunctionShortText"),
				LogicalChildren<Viewbox>(window).SingleOrDefault(view=>view.Name == "ContinuityFunctionSymbol")
					?? throw new Exception("Pole pomiaru nie ma symbolu ciągłości"),
				MeasurementFunction.Continuity)
		];
		foreach((TextBlock shortText,Viewbox symbol,MeasurementFunction function) in functionSymbols)
		{
			MeasurementSnapshot snapshot=new(
				new(function,null),
				new(ReadingState.Value,1d),
				1,
				new(1,1d,1d,1d,0d,0d),
				DateTimeOffset.UtcNow);
			applySnapshot.Invoke(multimeter,[snapshot]);
			if(shortText.Visibility != Visibility.Collapsed || symbol.Visibility != Visibility.Visible)
			{
				throw new Exception("Pole pomiaru pokazuje tekst zamiast symbolu funkcji "+function);
			}
		}
		applySnapshot.Invoke(multimeter,
		[
			new MeasurementSnapshot(
				new(MeasurementFunction.VoltageDc,null),
				new(ReadingState.Value,1d),
				1,
				new(1,1d,1d,1d,0d,0d),
				DateTimeOffset.UtcNow)
		]);
		TextBlock functionShortText=functionSymbols[0].ShortText;
		if(functionShortText.Visibility != Visibility.Visible || functionShortText.Text != "V DC" ||
			functionSymbols.Any(item=>item.Symbol.Visibility != Visibility.Collapsed))
		{
			throw new Exception("Tekstowe oznaczenie zwykłej funkcji pomiarowej nie zostało zachowane");
		}
		if(LogicalChildren<TextBlock>(window).Any(text=>text.Name == "IdentityText"))
		{
			throw new Exception("Interfejs nadal wyświetla identyfikację urządzenia obok połączenia");
		}
		Border measurementPanel=LogicalChildren<Border>(window)
			.Single(border=>border.Name == "MeasurementPanel");
		Border statisticsPanel=LogicalChildren<Border>(window)
			.Single(border=>border.Name == "StatisticsPanel");
		TextBlock primaryValue=LogicalChildren<TextBlock>(window)
			.Single(text=>text.Name == "PrimaryValueText");
		double primaryTop=primaryValue.TranslatePoint(new Point(0,0),measurementPanel).Y;
		double primaryBottom=primaryTop+primaryValue.ActualHeight;
		if(primaryTop<0d || primaryBottom>measurementPanel.ActualHeight)
		{
			throw new Exception("Główny odczyt wychodzi poza blok pomiaru");
		}
		double measurementBottom=measurementPanel.TranslatePoint(
			new Point(0,measurementPanel.ActualHeight),window).Y;
		double statisticsTop=statisticsPanel.TranslatePoint(new Point(0,0),window).Y;
		if(Math.Abs(statisticsTop-measurementBottom)>1)
		{
			throw new Exception(
				$"Statystyki nie stykają się z polem pomiaru: pomiar={measurementBottom:G}, statystyki={statisticsTop:G}");
		}
		TextBlock status=LogicalChildren<TextBlock>(window)
			.Single(text=>text.Name == "StatusText");
		Equal("Status: OFFLINE",status.Text);
		if(status.Foreground is not SolidColorBrush foreground || foreground.Color != Colors.White)
		{
			throw new Exception("Standardowy status nie jest biały");
		}
		Equal(12d,status.FontSize);
		Equal("Consolas",status.FontFamily.Source);
		Equal(FontWeights.Normal,status.FontWeight);
		Equal(VerticalAlignment.Center,status.VerticalAlignment);
		foreach(string name in new[]{"FunctionNameText","PrimaryValueText","MinimumText","MaximumText","AverageText"})
		{
			TextBlock text=LogicalChildren<TextBlock>(window).Single(item=>item.Name == name);
			if(text.Foreground is not SolidColorBrush textForeground || textForeground.Color != Colors.White)
			{
				throw new Exception("Tekst "+name+" nie jest biały");
			}
		}
		MenuItem save=LogicalChildren<MenuItem>(window)
			.Single(item=>Equals(item.Header,"Zapisz jako"));
		Equal("CSV",LogicalChildren<MenuItem>(save).Single().Header);
		string iconPath=File.Exists("Siglent_SDM3055.ico")
			? "Siglent_SDM3055.ico"
			: Path.Combine("SDM3000 Viewer","Siglent_SDM3055.ico");
		BitmapDecoder icon=BitmapDecoder.Create(
			new Uri(Path.GetFullPath(iconPath)),
			BitmapCreateOptions.PreservePixelFormat,
			BitmapCacheOption.OnLoad);
		Equal(icon.Frames[0].PixelWidth,icon.Frames[0].PixelHeight);
		window.Measure(new Size(window.Width,window.Height));
		window.Arrange(new Rect(0,0,window.Width,window.Height));
		window.UpdateLayout();
		RenderTargetBitmap bitmap=new(
			(int)window.Width,
			(int)window.Height,
			96,
			96,
			PixelFormats.Pbgra32);
		bitmap.Render(window);
		PngBitmapEncoder encoder=new();
		encoder.Frames.Add(BitmapFrame.Create(bitmap));
		string output=Path.Combine(
			"SDM3000 Viewer",
			"artifacts",
			"qa",
			"sdm3000-viewer.png");
		Directory.CreateDirectory(Path.GetDirectoryName(output)!);
		using(FileStream stream=File.Create(output))
		{
			encoder.Save(stream);
		}
		window.Close();
		application.Shutdown();
	});
});

Test("Ustawienia SDM zachowują adres i Auto connect",()=>
{
	string directory=Path.Combine(
		Path.GetTempPath(),
		"LabStation-SdmSettings-"+Guid.NewGuid().ToString("N"));
	string path=Path.Combine(directory,"settings.json");
	try
	{
		JsonMultimeterSettingsStore store=new(
			path,
			new("192.168.200.131",false));
		Equal(new MultimeterSettings("192.168.200.131",false),store.Load());
		store.Save(new("192.168.200.50",true));
		Equal(new MultimeterSettings("192.168.200.50",true),store.Load());
	}
	finally
	{
		if(Directory.Exists(directory))
		{
			Directory.Delete(directory,true);
		}
	}
});

Console.WriteLine($"Wynik: {passed} zaliczonych, {failed} niezaliczonych");
return failed == 0 ? 0 : 1;

sealed class RecordingTransport : IInstrumentTransport,ILocalControlTransport
{
	public List<string> Queries { get; }=[];
	public List<string> Writes { get; }=[];
	public int LocalRequests { get;private set; }
	public string? FailOnQuery { get;set; }
	public bool Disposed { get;private set; }

	public void Write(string command)
	{
		Writes.Add(command);
	}

	public byte[] Query(string command)
	{
		Queries.Add(command);
		if(command == FailOnQuery)
		{
			throw new IOException("Błąd testowy");
		}
		string response=command switch
		{
			"*IDN?"=>"SIGLENT,SDM3055,123456,1.01",
			"CONFigure?"=>"VOLT:AC 2.000000E+02",
			"READ?"=>"1.234500E+00",
			"DATA:POINts?"=>"17",
			_=>throw new InvalidOperationException(command)
		};
		return Encoding.ASCII.GetBytes(response);
	}

	public void Dispose()
	{
		Disposed=true;
	}

	public void ReturnToLocal()
	{
		LocalRequests++;
	}
}
