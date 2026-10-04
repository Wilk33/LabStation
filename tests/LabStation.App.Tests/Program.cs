using Ka3005P.App.Services;
using Ka3005P.App.ViewModels;
using Ka3005P.Core.Measurements;
using LabStation.App;
using LabStation.App.Korad;
using LabStation.UI;

int passed=0;
int failed=0;

async Task TestAsync(string name,Func<Task> action)
{
	try
	{
		await action();
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
		throw new InvalidOperationException(
			$"Oczekiwano {expected}, otrzymano {actual}");
	}
}

await TestAsync("Układ wariantu 1 zachowuje proporcje pięciu paneli",() =>
{
	PanelLayout layout=PanelLayout.Default;
	Equal(0.53,layout.OscilloscopeWidth);
	Equal(0.18,layout.GeneratorWidth);
	Equal(0.29,layout.RightColumnWidth);
	Equal(0.40,layout.MultimeterHeight);
	Equal(0.60,layout.ElectronicLoadHeight);
	Equal(1.00,
		Math.Round(
			layout.OscilloscopeWidth+
			layout.GeneratorWidth+
			layout.RightColumnWidth,
			2));
	return Task.CompletedTask;
});

await TestAsync("Dwa niezależne Korady otrzymują dwa jednoznaczne pliki",() =>
{
	KoradExportPaths paths=KoradExportPaths.ForConfiguration(
		@"C:\Pomiary\korad.csv",
		KoradConfiguration.TwoSingle);
	Equal(@"C:\Pomiary\korad-P1.csv",paths.PrimaryPath);
	Equal(@"C:\Pomiary\korad-P2.csv",paths.SecondaryPath);
	return Task.CompletedTask;
});

await TestAsync("Konfiguracja Korada tworzy jeden Single, dwa Single i Dual",async() =>
{
	FakeModeFactory factory=new();
	FakePortMonitor monitor=new(["COM3","COM4"]);
	MemorySettingsStore settings=new(new(
		KoradConfiguration.OneSingle,
		"COM3",
		"COM4",
		"COM3",
		"COM4"));
	await using KoradWorkspace workspace=new(factory,monitor,settings);
	await workspace.InitializeAsync(CancellationToken.None);
	Equal(KoradConfiguration.OneSingle,workspace.CurrentConfiguration);
	Equal(1,workspace.Modes.Count);

	Equal(true,await workspace.SetConfigurationAsync(
		KoradConfiguration.TwoSingle,
		CancellationToken.None));
	Equal(2,workspace.Modes.Count);
	Equal(3,factory.SingleCreated);

	Equal(true,await workspace.SetConfigurationAsync(
		KoradConfiguration.Dual,
		CancellationToken.None));
	Equal(1,workspace.Modes.Count);
	Equal(ApplicationMode.Dual,workspace.Modes[0].ApplicationMode);
});

await TestAsync("Aktywne wyjście blokuje zmianę konfiguracji Korada",async() =>
{
	FakeModeFactory factory=new();
	FakePortMonitor monitor=new(["COM3","COM4"]);
	MemorySettingsStore settings=new(KoradWorkspaceSettings.Default);
	await using KoradWorkspace workspace=new(factory,monitor,settings);
	await workspace.InitializeAsync(CancellationToken.None);
	((FakeMode)workspace.Modes[0]).OutputOn=true;

	Equal(false,await workspace.SetConfigurationAsync(
		KoradConfiguration.Dual,
		CancellationToken.None));
	Equal(KoradConfiguration.OneSingle,workspace.CurrentConfiguration);
});

await TestAsync("Kontrola wizualna może wyłączyć Auto connect bez zmiany ustawień",() =>
{
	string? previous=Environment.GetEnvironmentVariable(
		"LABSTATION_DISABLE_AUTO_CONNECT");
	try
	{
		Environment.SetEnvironmentVariable(
			"LABSTATION_DISABLE_AUTO_CONNECT",
			"1");
		Equal(false,StartupPolicy.AutoConnectAllowed);
		Environment.SetEnvironmentVariable(
			"LABSTATION_DISABLE_AUTO_CONNECT",
			"0");
		Equal(true,StartupPolicy.AutoConnectAllowed);
	}
	finally
	{
		Environment.SetEnvironmentVariable(
			"LABSTATION_DISABLE_AUTO_CONNECT",
			previous);
	}
	return Task.CompletedTask;
});

Console.WriteLine($"Wynik: {passed} zaliczonych, {failed} niezaliczonych");
return failed == 0 ? 0 : 1;

sealed class FakeModeFactory : IKoradWorkspaceModeFactory
{
	public int SingleCreated { get;private set; }

	public ISupplyModeViewModel CreateSingle(
		IReadOnlyList<string> ports,
		string? preferredPort)
	{
		SingleCreated++;
		return new FakeMode(ApplicationMode.Single,preferredPort ?? ports.FirstOrDefault());
	}

	public ISupplyModeViewModel CreateDual(
		IReadOnlyList<string> ports,
		string? firstPort,
		string? secondPort)
	{
		return new FakeMode(ApplicationMode.Dual,firstPort,secondPort);
	}
}

sealed class FakeMode(
	ApplicationMode mode,
	string? primaryPort,
	string? secondaryPort=null) : ISupplyModeViewModel
{
	public event EventHandler<bool>? OutputStateChanged;
	public event EventHandler<bool>? ConnectionStateChanged
	{
		add { }
		remove { }
	}
	public event EventHandler<ChartSample>? ChartSampleReceived
	{
		add { }
		remove { }
	}
	public ApplicationMode ApplicationMode=>mode;
	public string? PrimaryPort=>primaryPort;
	public string? SecondaryPort=>secondaryPort;
	public bool OutputOn { get;set; }
	public bool IsOutputOn=>OutputOn;
	public bool IsConnected=>false;
	public bool Closed { get;private set; }

	public void UpdateAvailablePorts(IReadOnlyList<string> ports)
	{
	}

	public ChartViewModel CreateChartViewModel(IFileDialogService fileDialog)=>new(this);

	public ValueTask SetOutputAsync(bool enabled,CancellationToken cancellationToken)
	{
		OutputOn=enabled;
		OutputStateChanged?.Invoke(this,enabled);
		return ValueTask.CompletedTask;
	}

	public ValueTask ExportAsync(
		MeasurementExportKind kind,
		string path,
		CancellationToken cancellationToken)=>ValueTask.CompletedTask;

	public ValueTask CloseAsync(CancellationToken cancellationToken)
	{
		Closed=true;
		return ValueTask.CompletedTask;
	}
}

sealed class FakePortMonitor(IReadOnlyList<string> ports) : ISerialPortMonitor
{
	public event SerialPortsChangedEventHandler? PortsChanged
	{
		add { }
		remove { }
	}
	public IReadOnlyList<string> Ports { get; }=ports;
	public Exception? LastError=>null;
	public void Start()
	{
	}
	public ValueTask RefreshOnceAsync(CancellationToken cancellationToken)=>ValueTask.CompletedTask;
	public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
}

sealed class MemorySettingsStore(KoradWorkspaceSettings settings)
	: IKoradWorkspaceSettingsStore
{
	public KoradWorkspaceSettings Value { get;private set; }=settings;

	public ValueTask<KoradWorkspaceSettings> LoadAsync(
		CancellationToken cancellationToken)=>ValueTask.FromResult(Value);

	public ValueTask SaveAsync(
		KoradWorkspaceSettings value,
		CancellationToken cancellationToken)
	{
		Value=value;
		return ValueTask.CompletedTask;
	}
}
