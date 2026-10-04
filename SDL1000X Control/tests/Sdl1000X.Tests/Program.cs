using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LabStation.Instruments.Discovery;
using LabStation.Instruments.Scpi;
using LabStation.Instruments.Transport;
using LabStation.UI.Controls;
using Sdl1000X.App;
using Sdl1000X.Core;

int passed=0;
int failed=0;

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

void AsyncTest(string name,Func<Task> action)
{
	Test(name,()=>action().GetAwaiter().GetResult());
}

void Equal<T>(T expected,T actual)
{
	if(!EqualityComparer<T>.Default.Equals(expected,actual))
	{
		throw new Exception($"Oczekiwano {expected}, otrzymano {actual}");
	}
}

TException Throws<TException>(Action action) where TException:Exception
{
	try
	{
		action();
	}
	catch(TException exception)
	{
		return exception;
	}
	throw new Exception("Oczekiwano wyjątku "+typeof(TException).Name);
}

IEnumerable<T> LogicalChildren<T>(DependencyObject root) where T:DependencyObject
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

Test("Profile statyczne zachowują limity modelu SDL1020X-E",()=>
{
	Equal(new LoadModeProfile("CC","A",0,30,0.001,3),LoadModeProfiles.For(LoadMode.ConstantCurrent));
	Equal(new LoadModeProfile("CV","V",0,150,0.001,3),LoadModeProfiles.For(LoadMode.ConstantVoltage));
	Equal(new LoadModeProfile("CP","W",0,200,0.01,2),LoadModeProfiles.For(LoadMode.ConstantPower));
	Equal(new LoadModeProfile("CR","Ω",0.03,10000,0.001,3),LoadModeProfiles.For(LoadMode.ConstantResistance));
});

Test("Protokół mapuje pięć trybów i polecenia ich nastaw",()=>
{
	Equal(LoadMode.ConstantCurrent,ElectronicLoadProtocol.ParseMode("CURRENT"));
	Equal(LoadMode.ConstantVoltage,ElectronicLoadProtocol.ParseMode(" voltage\r\n"));
	Equal(LoadMode.ConstantPower,ElectronicLoadProtocol.ParseMode("POWER"));
	Equal(LoadMode.ConstantResistance,ElectronicLoadProtocol.ParseMode("RESISTANCE"));
	Equal(LoadMode.Led,ElectronicLoadProtocol.ParseMode("LED"));
	Equal(":SOURce:FUNCtion CURRent",ElectronicLoadProtocol.ModeCommand(LoadMode.ConstantCurrent));
	Equal(":SOURce:CURRent:LEVel:IMMediate?",ElectronicLoadProtocol.SetpointQuery(LoadMode.ConstantCurrent));
	Equal(":SOURce:VOLTage:LEVel:IMMediate?",ElectronicLoadProtocol.SetpointQuery(LoadMode.ConstantVoltage));
	Equal(":SOURce:POWer:LEVel:IMMediate?",ElectronicLoadProtocol.SetpointQuery(LoadMode.ConstantPower));
	Equal(":SOURce:RESistance:LEVel:IMMediate?",ElectronicLoadProtocol.SetpointQuery(LoadMode.ConstantResistance));
});

Test("Polecenia liczbowe SCPI są niezależne od kultury",()=>
{
	CultureInfo previous=CultureInfo.CurrentCulture;
	try
	{
		CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("pl-PL");
		Equal(
			":SOURce:CURRent:LEVel:IMMediate 1.234",
			ElectronicLoadProtocol.SetpointCommand(LoadMode.ConstantCurrent,1.234));
		Equal(
			":SOURce:POWer:PROTection:LEVel 125.5",
			ElectronicLoadProtocol.OverPowerLevelCommand(125.5));
	}
	finally
	{
		CultureInfo.CurrentCulture=previous;
	}
});

Test("Parser odrzuca nieznany tryb oraz liczby NaN i nieskończone",()=>
{
	Throws<InvalidDataException>(()=>ElectronicLoadProtocol.ParseMode("BATTERY"));
	Throws<InvalidDataException>(()=>ElectronicLoadProtocol.ParseNumber("NaN","test"));
	Throws<InvalidDataException>(()=>ElectronicLoadProtocol.ParseNumber("Infinity","test"));
	Throws<InvalidDataException>(()=>ElectronicLoadProtocol.ParseNumber("1,25","test"));
});

Test("Walidacja odrzuca nastawy poza bezpiecznymi limitami",()=>
{
	Throws<ArgumentOutOfRangeException>(()=>ElectronicLoadProtocol.SetpointCommand(LoadMode.ConstantCurrent,30.001));
	Throws<ArgumentOutOfRangeException>(()=>ElectronicLoadProtocol.SetpointCommand(LoadMode.ConstantVoltage,150.001));
	Throws<ArgumentOutOfRangeException>(()=>ElectronicLoadProtocol.SetpointCommand(LoadMode.ConstantPower,200.01));
	Throws<ArgumentOutOfRangeException>(()=>ElectronicLoadProtocol.SetpointCommand(LoadMode.ConstantResistance,0.029));
	Throws<ArgumentOutOfRangeException>(()=>ElectronicLoadProtocol.OverCurrentLevelCommand(31.001));
	Throws<ArgumentOutOfRangeException>(()=>ElectronicLoadProtocol.OverPowerLevelCommand(210.001));
});

Test("Klient identyfikuje rodzinę SDL1020X i nie zapisuje nic podczas połączenia",()=>
{
	using SimulatedSdlTransport transport=new();
	using SiglentElectronicLoadClient client=new(transport);
	var identity=client.Initialize();
	Equal("SIGLENT",identity.Manufacturer);
	Equal("SDL1020X-E",identity.Model);
	Equal(0,transport.Writes.Count);
	Equal("*IDN?",transport.Queries.Single());
});

Test("Klient odczytuje pełny stan z symulatora",()=>
{
	using SimulatedSdlTransport transport=new()
	{
		Mode=LoadMode.ConstantPower,
		InputEnabled=true,
		Voltage=12.125,
		Current=1.5,
		Power=18.1875,
		Resistance=8.083333,
		OverCurrentEnabled=true,
		OverCurrentAmps=4.5,
		OverPowerEnabled=true,
		OverPowerWatts=60
	};
	transport.Setpoints[LoadMode.ConstantPower]=20;
	using SiglentElectronicLoadClient client=new(transport);
	client.Initialize();
	ElectronicLoadSnapshot snapshot=client.ReadSnapshot();
	Equal(LoadMode.ConstantPower,snapshot.Mode);
	Equal(true,snapshot.InputEnabled);
	Equal(20d,snapshot.Setpoint);
	Equal(new LoadMeasurements(12.125,1.5,18.1875,8.083333),snapshot.Measurements);
	Equal(new ProtectionSettings(true,4.5,true,60),snapshot.Protections);
});

Test("Tryb LED odczytuje trzy niezależne nastawy",()=>
{
	using SimulatedSdlTransport transport=new()
	{
		Mode=LoadMode.Led,
		LedVoltage=3.2,
		LedCurrent=0.7,
		LedResistance=0.15
	};
	using SiglentElectronicLoadClient client=new(transport);
	client.Initialize();
	ElectronicLoadSnapshot snapshot=client.ReadSnapshot();
	Equal(new LedSettings(3.2,0.7,0.15),snapshot.Led);
	Equal<double?>(null,snapshot.Setpoint);
});

Test("Błędna odpowiedź pomiarowa nie jest zamieniana na zero",()=>
{
	using SimulatedSdlTransport transport=new();
	transport.QueryOverrides["MEASure:VOLTage:DC?"]="BROKEN";
	using SiglentElectronicLoadClient client=new(transport);
	client.Initialize();
	Throws<InvalidDataException>(()=>client.ReadSnapshot());
	transport.QueryOverrides.Remove("MEASure:VOLTage:DC?");
	Equal(12d,client.ReadSnapshot().Measurements.Voltage);
});

Test("Zmiana nastawy jest blokowana gdy wejście pozostaje włączone",()=>
{
	using SimulatedSdlTransport transport=new(){InputEnabled=true};
	using SiglentElectronicLoadClient client=new(transport);
	client.Initialize();
	Throws<InvalidOperationException>(()=>client.SetSetpoint(LoadMode.ConstantCurrent,2));
	Equal(0,transport.Writes.Count);
	Equal(":SOURce:INPut:STATe?",transport.Queries[^1]);
});

Test("Wyłączone wejście pozwala ustawić tryb, nastawę i zabezpieczenia",()=>
{
	using SimulatedSdlTransport transport=new();
	using SiglentElectronicLoadClient client=new(transport);
	client.Initialize();
	client.SetMode(LoadMode.ConstantVoltage);
	client.SetSetpoint(LoadMode.ConstantVoltage,24.5);
	client.SetProtections(new(true,5.25,true,100));
	Equal(LoadMode.ConstantVoltage,transport.Mode);
	Equal(24.5,transport.Setpoints[LoadMode.ConstantVoltage]);
	Equal(true,transport.OverCurrentEnabled);
	Equal(5.25,transport.OverCurrentAmps);
	Equal(true,transport.OverPowerEnabled);
	Equal(100d,transport.OverPowerWatts);
});

Test("ON i OFF są potwierdzane ponownym odczytem stanu",()=>
{
	using SimulatedSdlTransport transport=new();
	using SiglentElectronicLoadClient client=new(transport);
	client.Initialize();
	client.SetInput(true);
	Equal(true,transport.InputEnabled);
	client.SetInput(false);
	Equal(false,transport.InputEnabled);
	Equal(2,transport.Writes.Count(command=>command.StartsWith(":SOURce:INPut:STATe ",StringComparison.Ordinal)));
	if(transport.Queries.Count(command=>command == ":SOURce:INPut:STATe?")<4)
	{
		throw new Exception("Nie wykonano odczytu przed i po zmianie wejścia");
	}
});

AsyncTest("Sesja odrzuca inny model i zamyka transport",async()=>
{
	SimulatedSdlTransport transport=new(){Identity="SIGLENT,SDL1030X,OTHER,1.0"};
	await ThrowsAsync<InvalidDataException>(
		()=>ElectronicLoadSession.CreateAsync(()=>transport));
	Equal(true,transport.Disposed);
});

AsyncTest("Połączenie sesji tylko identyfikuje urządzenie i nie włącza wejścia",async()=>
{
	SimulatedSdlTransport transport=new();
	await using ElectronicLoadSession session=await ElectronicLoadSession.CreateAsync(()=>transport);
	Equal("SDL1020X-E",session.Identity.Model);
	Equal(false,transport.InputEnabled);
	Equal(0,transport.Writes.Count);
});

AsyncTest("Sesja serializuje równoległy odczyt i zapis",async()=>
{
	SimulatedSdlTransport transport=new(){OperationDelay=TimeSpan.FromMilliseconds(10)};
	await using ElectronicLoadSession session=await ElectronicLoadSession.CreateAsync(()=>transport);
	Task read=session.ReadAsync();
	Task write=session.SetSetpointAsync(LoadMode.ConstantCurrent,2.5);
	await Task.WhenAll(read,write);
	Equal(1,transport.MaximumConcurrentOperations);
});

AsyncTest("Priorytetowe OFF wyprzedza oczekującą nastawę",async()=>
{
	SimulatedSdlTransport transport=new()
	{
		BlockOnWritePrefix=":SOURce:CURRent:LEVel:IMMediate "
	};
	await using ElectronicLoadSession session=await ElectronicLoadSession.CreateAsync(()=>transport);
	Task first=session.SetSetpointAsync(LoadMode.ConstantCurrent,2);
	if(!transport.WriteEntered.Wait(TimeSpan.FromSeconds(2)))
	{
		throw new TimeoutException("Pierwsza nastawa nie weszła do symulatora.");
	}
	transport.InputEnabled=true;
	Task second=session.SetSetpointAsync(LoadMode.ConstantVoltage,15);
	Task off=session.SetInputAsync(false);
	transport.WriteRelease.Set();
	await Task.WhenAll(first,second,off);
	int firstIndex=transport.Writes.FindIndex(command=>command.Contains("CURRent:LEVel",StringComparison.Ordinal));
	int offIndex=transport.Writes.FindIndex(command=>command.EndsWith(" OFF",StringComparison.Ordinal));
	int secondIndex=transport.Writes.FindIndex(command=>command.Contains("VOLTage:LEVel",StringComparison.Ordinal));
	if(firstIndex<0 || offIndex<=firstIndex || secondIndex<=offIndex)
	{
		throw new Exception("Kolejność zapisów nie zachowała priorytetu OFF: "+string.Join(" | ",transport.Writes));
	}
});

AsyncTest("Zamknięcie sesji wyłącza aktywne wejście i weryfikuje OFF",async()=>
{
	SimulatedSdlTransport transport=new(){InputEnabled=true};
	ElectronicLoadSession session=await ElectronicLoadSession.CreateAsync(()=>transport);
	await session.DisposeAsync();
	Equal(false,transport.InputEnabled);
	Equal(true,transport.Disposed);
	if(!transport.Writes.Contains(":SOURce:INPut:STATe OFF"))
	{
		throw new Exception("Zamknięcie nie wysłało OFF.");
	}
});

AsyncTest("Sesja odzyskuje odczyt po przejściowym błędzie transportu",async()=>
{
	SimulatedSdlTransport transport=new();
	transport.QueryFailuresRemaining["MEASure:POWer:DC?"]=1;
	await using ElectronicLoadSession session=await ElectronicLoadSession.CreateAsync(()=>transport);
	await ThrowsAsync<IOException>(()=>session.ReadAsync());
	ElectronicLoadSnapshot snapshot=await session.ReadAsync();
	Equal(12d,snapshot.Measurements.Power);
});

AsyncTest("ON jest blokowane gdy odczytana nastawa przekracza limit",async()=>
{
	SimulatedSdlTransport transport=new(){Mode=LoadMode.ConstantPower};
	transport.QueryOverrides[":SOURce:POWer:LEVel:IMMediate?"]="250";
	await using ElectronicLoadSession session=await ElectronicLoadSession.CreateAsync(()=>transport);
	await ThrowsAsync<ArgumentOutOfRangeException>(()=>session.SetInputAsync(true));
	Equal(false,transport.InputEnabled);
	Equal(false,transport.Writes.Any(command=>command.EndsWith(" ON",StringComparison.Ordinal)));
});

AsyncTest("Brak potwierdzenia zmiany wejścia jest błędem komunikacji",async()=>
{
	SimulatedSdlTransport transport=new(){IgnoreInputWrites=true};
	await using ElectronicLoadSession session=await ElectronicLoadSession.CreateAsync(()=>transport);
	await ThrowsAsync<IOException>(()=>session.SetInputAsync(true));
	Equal(false,transport.InputEnabled);
});

Test("Ustawienia SDL zachowują adres i Auto connect",()=>
{
	string directory=Path.Combine(
		Path.GetTempPath(),
		"LabStation-SdlSettings-"+Guid.NewGuid().ToString("N"));
	string path=Path.Combine(directory,"settings.json");
	try
	{
		JsonElectronicLoadSettingsStore store=new(path,new("",false));
		Equal(new ElectronicLoadSettings("",false),store.Load());
		store.Save(new("192.168.200.77",true));
		Equal(new ElectronicLoadSettings("192.168.200.77",true),store.Load());
	}
	finally
	{
		if(Directory.Exists(directory))
		{
			Directory.Delete(directory,true);
		}
	}
});

using StaTestHost sta=new();

Test("Skan wpisuje adres SDL i Auto connect nawiązuje bezpieczne połączenie",()=>
{
	sta.InvokeAsync(async()=>
	{
		SimulatedSdlTransport transport=new();
		ElectronicLoadView view=new(
			new FixedScanner(new(
				"192.168.200.77",
				new("SIGLENT","SDL1020X-E","SIM","1.0"))),
			_=>transport,
			new MemorySettingsStore(new("",true)),
			TimeSpan.FromMilliseconds(20));
		await view.ScanNetworkAsync();
		Equal("192.168.200.77",view.HostAddress);
		Equal(true,view.IsConnected);
		Equal(false,transport.InputEnabled);
		Equal(0,transport.Writes.Count);
		await view.DisposeAsync();
	});
});

Test("Panel cyklicznie odświeża pomiary i tryb zmienione po stronie urządzenia",()=>
{
	sta.InvokeAsync(async()=>
	{
		SimulatedSdlTransport transport=new();
		ElectronicLoadView view=new(
			new FixedScanner(new(
				"192.168.200.77",
				new("SIGLENT","SDL1020X-E","SIM","1.0"))),
			_=>transport,
			new MemorySettingsStore(new("",true)),
			TimeSpan.FromMilliseconds(20));
		await view.ScanNetworkAsync();
		transport.Mode=LoadMode.ConstantResistance;
		transport.Setpoints[LoadMode.ConstantResistance]=47.5;
		transport.Voltage=8.25;
		transport.Current=0.175;
		transport.Power=1.44375;
		transport.Resistance=47.142857;
		await WaitUntilAsync(
			()=>view.CurrentSnapshot is
			{
				Mode:LoadMode.ConstantResistance,
				Measurements.Voltage:8.25
			},
			TimeSpan.FromSeconds(2));
		Equal(47.5,view.CurrentSnapshot!.Setpoint);
		Equal(0.175,view.CurrentSnapshot.Measurements.Current);
		Equal(1.44375,view.CurrentSnapshot.Measurements.Power);
		Equal(47.142857,view.CurrentSnapshot.Measurements.Resistance);
		await view.DisposeAsync();
	});
});

Test("Metadane i osadzona licencja są zgodne z aplikacjami LabStation",()=>
{
	Equal("Siglent SDL1000X Control v0.1.0",AppInformation.DisplayName);
	Equal("Mateusz Skipor",AppInformation.AuthorName);
	string license=AppInformation.LoadLicenseText();
	if(!license.Contains("PolyForm Noncommercial License 1.0.0",StringComparison.Ordinal) ||
		!license.Contains("that would otherwise infringe the licensor's copyright",StringComparison.Ordinal))
	{
		throw new Exception("Osadzony tekst licencji jest niekompletny lub nieprawidłowy.");
	}
});

Test("Panel SDL wdraża kompaktowy wariant nastawczy bez wykresu",()=>
{
	sta.Invoke(()=>
	{
		MainWindow window=new();
		window.Show();
		window.UpdateLayout();
		Equal("0.1.0",AppInformation.Version);
		Equal("Siglent SDL1000X Control v0.1.0",window.Title);
		Equal(520d,window.Width);
		if(window.Height<390d || window.Height>420d)
		{
			throw new Exception("Okno SDL nie mieści się w przestrzeni pod Multimetrem.");
		}
		Equal(ResizeMode.CanMinimize,window.ResizeMode);
		TextBox host=LogicalChildren<TextBox>(window).Single(item=>item.Name == "HostEditor");
		Equal("",host.Text);
		Equal(150d,host.Width);
		Button connection=LogicalChildren<Button>(window).Single(item=>item.Name == "ConnectionButton");
		Equal("Offline",connection.Content);
		Button input=LogicalChildren<Button>(window).Single(item=>item.Name == "InputButton");
		Equal("OFF",input.Content);
		UniformGrid modes=LogicalChildren<UniformGrid>(window).Single(item=>item.Name == "ModeButtons");
		Equal(5,modes.Columns);
		string[] tags=LogicalChildren<Button>(modes)
			.Select(button=>button.Tag?.ToString() ?? "")
			.ToArray();
		if(!new[]{"ConstantCurrent","ConstantVoltage","ConstantPower","ConstantResistance","Led"}.SequenceEqual(tags))
		{
			throw new Exception("Panel nie zawiera trybów CC, CV, CP, CR i LED.");
		}
		foreach(string name in new[]{"VoltageValueText","CurrentValueText","PowerValueText","ResistanceValueText"})
		{
			_ = LogicalChildren<TextBlock>(window).Single(text=>text.Name == name);
		}
		NumericValueEditor[] editors=LogicalChildren<NumericValueEditor>(window).ToArray();
		foreach(string name in new[]{"SetpointEditor","LedVoltageEditor","LedCurrentEditor","LedResistanceEditor","OverCurrentEditor","OverPowerEditor"})
		{
			_ = editors.Single(editor=>editor.Name == name);
		}
		TextBlock ovp=LogicalChildren<TextBlock>(window).Single(text=>text.Name == "OverVoltageText");
		TextBlock otp=LogicalChildren<TextBlock>(window).Single(text=>text.Name == "OverTemperatureText");
		Equal("155 V",ovp.Text);
		Equal("85 °C",otp.Text);
		if(LogicalChildren<TimeSeriesPlot>(window).Any())
		{
			throw new Exception("Panel SDL nie powinien zawierać wykresu.");
		}
		TextBlock status=LogicalChildren<TextBlock>(window).Single(text=>text.Name == "StatusText");
		Equal("Status: OFFLINE",status.Text);
		if(status.Foreground is not SolidColorBrush brush || brush.Color != Colors.White)
		{
			throw new Exception("Zwykły status nie jest biały.");
		}
		MenuItem tools=LogicalChildren<MenuItem>(window).Single(item=>Equals(item.Header,"Narzędzia"));
		string[] toolItems=LogicalChildren<MenuItem>(tools).Select(item=>item.Header?.ToString() ?? "").ToArray();
		if(!toolItems.Contains("Skanuj sieć") || !toolItems.Contains("Auto connect"))
		{
			throw new Exception("Menu Narzędzia nie zawiera skanowania i Auto connect.");
		}
		if(!LogicalChildren<MenuItem>(window).Any(item=>Equals(item.Header,"O aplikacji")))
		{
			throw new Exception("Brak wspólnego menu O aplikacji.");
		}
		string iconPath=File.Exists("SDL1020X.ico")
			? "SDL1020X.ico"
			: Path.Combine("SDL1000X Control","SDL1020X.ico");
		BitmapDecoder icon=BitmapDecoder.Create(
			new Uri(Path.GetFullPath(iconPath)),
			BitmapCreateOptions.PreservePixelFormat,
			BitmapCacheOption.OnLoad);
		int[] sizes=icon.Frames.Select(frame=>frame.PixelWidth).Order().ToArray();
		if(!new[]{16,32,256}.All(size=>sizes.Contains(size)))
		{
			throw new Exception("Ikona EXE nie zawiera wariantów 16, 32 i 256 px.");
		}
		RenderTargetBitmap bitmap=new(
			(int)window.Width,
			(int)window.Height,
			96,
			96,
			PixelFormats.Pbgra32);
		bitmap.Render(window);
		PngBitmapEncoder encoder=new();
		encoder.Frames.Add(BitmapFrame.Create(bitmap));
		string output=Path.Combine("SDL1000X Control","artifacts","qa","sdl1000x-control.png");
		Directory.CreateDirectory(Path.GetDirectoryName(output)!);
		using(FileStream stream=File.Create(output))
		{
			encoder.Save(stream);
		}
		window.Close();
	});
});

Console.WriteLine($"Wynik: {passed} zaliczonych, {failed} niezaliczonych");
return failed == 0 ? 0 : 1;

async Task<TException> ThrowsAsync<TException>(Func<Task> action) where TException:Exception
{
	try
	{
		await action();
	}
	catch(TException exception)
	{
		return exception;
	}
	throw new Exception("Oczekiwano wyjątku "+typeof(TException).Name);
}

async Task WaitUntilAsync(Func<bool> predicate,TimeSpan timeout)
{
	DateTime deadline=DateTime.UtcNow+timeout;
	while(!predicate())
	{
		if(DateTime.UtcNow>=deadline)
		{
			throw new TimeoutException("Warunek testu nie został spełniony w wyznaczonym czasie.");
		}
		await Task.Delay(10);
	}
}

sealed class SimulatedSdlTransport : IInstrumentTransport
{
	private int activeOperations;

	public string Identity { get;set; }="SIGLENT,SDL1020X-E,SDLTEST001,1.1.1.23R4";
	public LoadMode Mode { get;set; }=LoadMode.ConstantCurrent;
	public bool InputEnabled { get;set; }
	public Dictionary<LoadMode,double> Setpoints { get; }=new()
	{
		[LoadMode.ConstantCurrent]=1,
		[LoadMode.ConstantVoltage]=12,
		[LoadMode.ConstantPower]=10,
		[LoadMode.ConstantResistance]=10
	};
	public double LedVoltage { get;set; }=3;
	public double LedCurrent { get;set; }=0.5;
	public double LedResistance { get;set; }=0.2;
	public double Voltage { get;set; }=12;
	public double Current { get;set; }=1;
	public double Power { get;set; }=12;
	public double Resistance { get;set; }=12;
	public bool OverCurrentEnabled { get;set; }=true;
	public double OverCurrentAmps { get;set; }=30;
	public bool OverPowerEnabled { get;set; }=true;
	public double OverPowerWatts { get;set; }=200;
	public List<string> Queries { get; }=[];
	public List<string> Writes { get; }=[];
	public Dictionary<string,string> QueryOverrides { get; }=new(StringComparer.Ordinal);
	public Dictionary<string,int> QueryFailuresRemaining { get; }=new(StringComparer.Ordinal);
	public TimeSpan OperationDelay { get;set; }
	public string? BlockOnWritePrefix { get;set; }
	public ManualResetEventSlim WriteEntered { get; }=new(false);
	public ManualResetEventSlim WriteRelease { get; }=new(false);
	public bool IgnoreInputWrites { get;set; }
	public int MaximumConcurrentOperations { get;private set; }
	public bool Disposed { get;private set; }

	public void Write(string command)
	{
		EnterOperation();
		try
		{
			if(BlockOnWritePrefix is not null && command.StartsWith(BlockOnWritePrefix,StringComparison.Ordinal))
			{
				WriteEntered.Set();
				if(!WriteRelease.Wait(TimeSpan.FromSeconds(5)))
				{
					throw new TimeoutException("Testowy zapis nie został zwolniony.");
				}
				BlockOnWritePrefix=null;
			}
			Writes.Add(command);
			if(command.StartsWith(":SOURce:FUNCtion ",StringComparison.Ordinal))
			{
				Mode=ElectronicLoadProtocol.ParseMode(command.Split(' ')[1]);
				return;
			}
			if(command.StartsWith(":SOURce:INPut:STATe ",StringComparison.Ordinal))
			{
				if(!IgnoreInputWrites)
				{
					InputEnabled=ElectronicLoadProtocol.ParseBoolean(command.Split(' ')[1],command);
				}
				return;
			}
			if(TrySet(command,":SOURce:CURRent:LEVel:IMMediate ",value=>Setpoints[LoadMode.ConstantCurrent]=value) ||
				TrySet(command,":SOURce:VOLTage:LEVel:IMMediate ",value=>Setpoints[LoadMode.ConstantVoltage]=value) ||
				TrySet(command,":SOURce:POWer:LEVel:IMMediate ",value=>Setpoints[LoadMode.ConstantPower]=value) ||
				TrySet(command,":SOURce:RESistance:LEVel:IMMediate ",value=>Setpoints[LoadMode.ConstantResistance]=value) ||
				TrySet(command,":SOURce:LED:VOLTage ",value=>LedVoltage=value) ||
				TrySet(command,":SOURce:LED:CURRent ",value=>LedCurrent=value) ||
				TrySet(command,":SOURce:LED:RCOnf ",value=>LedResistance=value) ||
				TrySet(command,":SOURce:CURRent:PROTection:LEVel ",value=>OverCurrentAmps=value) ||
				TrySet(command,":SOURce:POWer:PROTection:LEVel ",value=>OverPowerWatts=value))
			{
				return;
			}
			if(command.StartsWith(":SOURce:CURRent:PROTection:STATe ",StringComparison.Ordinal))
			{
				OverCurrentEnabled=ElectronicLoadProtocol.ParseBoolean(command.Split(' ')[1],command);
				return;
			}
			if(command.StartsWith(":SOURce:POWer:PROTection:STATe ",StringComparison.Ordinal))
			{
				OverPowerEnabled=ElectronicLoadProtocol.ParseBoolean(command.Split(' ')[1],command);
				return;
			}
			throw new InvalidOperationException("Nieobsługiwany zapis symulatora: "+command);
		}
		finally
		{
			ExitOperation();
		}
	}

	public byte[] Query(string command)
	{
		EnterOperation();
		try
		{
			Queries.Add(command);
			if(QueryFailuresRemaining.TryGetValue(command,out int remaining) && remaining>0)
			{
				QueryFailuresRemaining[command]=remaining-1;
				throw new IOException("Symulowany błąd transportu dla "+command);
			}
			if(QueryOverrides.TryGetValue(command,out string? value))
			{
				return Encoding.ASCII.GetBytes(value);
			}
			string response=command switch
			{
				"*IDN?"=>Identity,
				":SOURce:INPut:STATe?"=>InputEnabled ? "1" : "0",
				":SOURce:FUNCtion?"=>ModeToken(Mode),
				":SOURce:CURRent:LEVel:IMMediate?"=>Number(Setpoints[LoadMode.ConstantCurrent]),
				":SOURce:VOLTage:LEVel:IMMediate?"=>Number(Setpoints[LoadMode.ConstantVoltage]),
				":SOURce:POWer:LEVel:IMMediate?"=>Number(Setpoints[LoadMode.ConstantPower]),
				":SOURce:RESistance:LEVel:IMMediate?"=>Number(Setpoints[LoadMode.ConstantResistance]),
				":SOURce:LED:VOLTage?"=>Number(LedVoltage),
				":SOURce:LED:CURRent?"=>Number(LedCurrent),
				":SOURce:LED:RCOnf?"=>Number(LedResistance),
				":SOURce:CURRent:PROTection:STATe?"=>OverCurrentEnabled ? "1" : "0",
				":SOURce:CURRent:PROTection:LEVel?"=>Number(OverCurrentAmps),
				":SOURce:POWer:PROTection:STATe?"=>OverPowerEnabled ? "1" : "0",
				":SOURce:POWer:PROTection:LEVel?"=>Number(OverPowerWatts),
				"MEASure:VOLTage:DC?"=>Number(Voltage),
				"MEASure:CURRent:DC?"=>Number(Current),
				"MEASure:POWer:DC?"=>Number(Power),
				"MEASure:RESistance:DC?"=>Number(Resistance),
				_=>throw new InvalidOperationException("Nieobsługiwane zapytanie symulatora: "+command)
			};
			return Encoding.ASCII.GetBytes(response+"\r\n");
		}
		finally
		{
			ExitOperation();
		}
	}

	public void Dispose()
	{
		Disposed=true;
	}

	private static string Number(double value)=>value.ToString("G17",CultureInfo.InvariantCulture);

	private static string ModeToken(LoadMode mode)
	{
		return mode switch
		{
			LoadMode.ConstantCurrent=>"CURRENT",
			LoadMode.ConstantVoltage=>"VOLTAGE",
			LoadMode.ConstantPower=>"POWER",
			LoadMode.ConstantResistance=>"RESISTANCE",
			LoadMode.Led=>"LED",
			_=>throw new ArgumentOutOfRangeException(nameof(mode))
		};
	}

	private static bool TrySet(string command,string prefix,Action<double> setter)
	{
		if(!command.StartsWith(prefix,StringComparison.Ordinal))
		{
			return false;
		}
		setter(double.Parse(command[prefix.Length..],CultureInfo.InvariantCulture));
		return true;
	}

	private void EnterOperation()
	{
		int active=Interlocked.Increment(ref activeOperations);
		MaximumConcurrentOperations=Math.Max(MaximumConcurrentOperations,active);
		if(OperationDelay>TimeSpan.Zero)
		{
			Thread.Sleep(OperationDelay);
		}
	}

	private void ExitOperation()
	{
		Interlocked.Decrement(ref activeOperations);
	}
}

sealed class FixedScanner(DiscoveredInstrument? result) : IInstrumentNetworkScanner
{
	public Task<DiscoveredInstrument?> FindFirstAsync(
		Func<ScpiIdentity,bool> isSupported,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(
			result is not null && isSupported(result.Identity)
				? result
				: null);
	}
}

sealed class MemorySettingsStore(ElectronicLoadSettings settings)
	: IElectronicLoadSettingsStore
{
	private ElectronicLoadSettings current=settings;
	public ElectronicLoadSettings Load()=>current;
	public void Save(ElectronicLoadSettings value)
	{
		current=value;
	}
}

sealed class StaTestHost : IDisposable
{
	private readonly ManualResetEventSlim ready=new(false);
	private readonly Thread thread;
	private Dispatcher? dispatcher;
	private Sdl1000X.App.App? application;
	private Exception? startupFailure;

	public StaTestHost()
	{
		thread=new Thread(Run);
		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();
		if(!ready.Wait(TimeSpan.FromSeconds(10)))
		{
			throw new TimeoutException("Host WPF nie uruchomił się.");
		}
		if(startupFailure is not null)
		{
			throw startupFailure;
		}
	}

	public void Invoke(Action action)
	{
		ArgumentNullException.ThrowIfNull(action);
		Dispatcher current=dispatcher ?? throw new ObjectDisposedException(nameof(StaTestHost));
		current.Invoke(action);
	}

	public void InvokeAsync(Func<Task> action)
	{
		ArgumentNullException.ThrowIfNull(action);
		Dispatcher current=dispatcher ?? throw new ObjectDisposedException(nameof(StaTestHost));
		current.InvokeAsync(action).Task.Unwrap().GetAwaiter().GetResult();
	}

	public void Dispose()
	{
		Dispatcher? current=dispatcher;
		if(current is null)
		{
			return;
		}
		current.Invoke(()=>application?.Shutdown());
		current.BeginInvokeShutdown(DispatcherPriority.Normal);
		thread.Join(TimeSpan.FromSeconds(10));
		dispatcher=null;
		ready.Dispose();
	}

	private void Run()
	{
		try
		{
			dispatcher=Dispatcher.CurrentDispatcher;
			SynchronizationContext.SetSynchronizationContext(
				new DispatcherSynchronizationContext(dispatcher));
			application=new Sdl1000X.App.App();
			application.InitializeComponent();
		}
		catch(Exception exception)
		{
			startupFailure=exception;
		}
		finally
		{
			ready.Set();
		}
		if(startupFailure is null)
		{
			Dispatcher.Run();
		}
	}
}
