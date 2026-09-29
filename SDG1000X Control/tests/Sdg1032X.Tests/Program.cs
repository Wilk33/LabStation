using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using LabStation.Instruments.Scheduling;
using LabStation.Instruments.Transport;
using LabStation.UI;
using LabStation.UI.Controls;
using Sdg1032X.App;
using Sdg1032X.App.Controls;
using Sdg1032X.Core;

int failed=0;
int passed=0;

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

Test("Lista przebiegów zawiera wyłącznie sześć podstawowych typów",()=>
{
	BasicWaveform[] values=Enum.GetValues<BasicWaveform>();
	Equal(6,values.Length);
	Equal("SINE",SiglentProtocol.WaveformCode(BasicWaveform.Sine));
	Equal("SQUARE",SiglentProtocol.WaveformCode(BasicWaveform.Square));
	Equal("RAMP",SiglentProtocol.WaveformCode(BasicWaveform.Ramp));
	Equal("PULSE",SiglentProtocol.WaveformCode(BasicWaveform.Pulse));
	Equal("NOISE",SiglentProtocol.WaveformCode(BasicWaveform.Noise));
	Equal("DC",SiglentProtocol.WaveformCode(BasicWaveform.Dc));
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
		"C1:BSWV WVTP,ARB",
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
	Equal("0.2.0",ProductInformation.Version);
	Equal("Siglent SDG1000X Control v0.2.0",ProductInformation.GetWindowTitle());
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
		MainWindow window=new();
		window.Measure(new Size(350,749));
		window.Arrange(new Rect(0,0,350,749));
		window.ApplyTemplate();

		Equal(749d,window.Height);
		GeneratorView generatorView=LogicalChildren<GeneratorView>(window).Single();
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
		if(host.Padding.Top>2 || host.Padding.Bottom>2)
		{
			throw new Exception("Pole IP ma zbyt duży pionowy margines wewnętrzny");
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
