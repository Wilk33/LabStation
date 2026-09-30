using System.Globalization;
using System.IO;
using System.Collections.ObjectModel;
using Ka3005P.App.Infrastructure;
using Ka3005P.App.Services;
using Ka3005P.Core.Configuration;
using Ka3005P.Core.Device;
using Ka3005P.Core.Dual;
using Ka3005P.Core.Measurements;
using Ka3005P.Core.Protocol;
using Ka3005P.Core.Sessions;

namespace Ka3005P.App.ViewModels;

public sealed class DualSupplyViewModel : ObservableObject,ISupplyModeViewModel
{
	private static readonly CultureInfo PolishCulture=
		CultureInfo.GetCultureInfo("pl-PL");

	private readonly PortLeaseRegistry? leases;
	private readonly ISingleSessionFactory? sessionFactory;
	private readonly SynchronizationContext? uiContext=SynchronizationContext.Current;
	private readonly object measurementGate=new();
	private readonly SemaphoreSlim operationGate=new(1,1);
	private readonly List<DualMeasurement> measurements=[];
	private DualPowerSupplyController? controller;
	private PortLease? firstLease;
	private PortLease? secondLease;
	private DualMode mode=DualMode.Series;
	private readonly List<string> portSnapshot=[];
	private string? selectedFirstPort;
	private string? selectedSecondPort;
	private bool updatingPorts;
	private string voltageText="12,00";
	private string currentText="1,000";
	private string measuredVoltageText="0,00 V";
	private string measuredCurrentText="0,000 A";
	private string firstMeasurementText="0,00 V / 0,000 A";
	private string secondMeasurementText="0,00 V / 0,000 A";
	private string firstSideVoltageText="+0,00 V";
	private string firstSideCurrentText="+0,000 A";
	private string secondSideVoltageText="-0,00 V";
	private string secondSideCurrentText="-0,000 A";
	private string? resistanceText;
	private string? errorMessage;
	private int voltageHundredths=1200;
	private int currentThousandths=1000;
	private bool isConnected;
	private bool isOutputOn;
	private bool isResistanceVisible;
	private bool setpointsAreValid=true;
	private bool closing;
	private int faultCleanupScheduled;

	public event EventHandler<bool>? OutputStateChanged;
	public event EventHandler<bool>? ConnectionStateChanged;
	public event EventHandler<ChartSample>? ChartSampleReceived;

	public DualSupplyViewModel(DualPowerSupplyController controller)
	{
		ArgumentNullException.ThrowIfNull(controller);
		this.controller=controller;
		mode=controller.Mode;
		isConnected=true;
		AttachController(controller);
		ConnectCommand=new AsyncRelayCommand(_=>Task.CompletedTask,_=>false);
		InitializeCommands();
		UpdatePhysicalSetpointTexts();
	}

	public DualSupplyViewModel(
		PortLeaseRegistry leases,
		ISingleSessionFactory sessionFactory,
		IReadOnlyList<string>? availablePorts=null,
		string? preferredFirstPort=null,
		string? preferredSecondPort=null)
	{
		ArgumentNullException.ThrowIfNull(leases);
		ArgumentNullException.ThrowIfNull(sessionFactory);
		this.leases=leases;
		this.sessionFactory=sessionFactory;
		UpdateAvailablePorts(availablePorts ?? ["COM5","COM6"]);
		RestorePreferredPorts(preferredFirstPort,preferredSecondPort);
		ConnectCommand=new AsyncRelayCommand(
			ToggleConnectionAsync,
			_=>IsConnected || CanConnect(),
			exception=>ErrorMessage=exception.Message);
		InitializeCommands();
		UpdatePhysicalSetpointTexts();
	}

	public AsyncRelayCommand ConnectCommand { get; private set; }=null!;
	public RelayCommand IncrementVoltageCommand { get; private set; }=null!;
	public RelayCommand DecrementVoltageCommand { get; private set; }=null!;
	public RelayCommand IncrementCurrentCommand { get; private set; }=null!;
	public RelayCommand DecrementCurrentCommand { get; private set; }=null!;
	public RelayCommand CommitVoltageCommand { get; private set; }=null!;
	public RelayCommand CommitCurrentCommand { get; private set; }=null!;
	public AsyncRelayCommand ToggleOutputCommand { get; private set; }=null!;
	public AsyncRelayCommand ChangeModeCommand { get; private set; }=null!;

	public DualMode Mode
	{
		get => mode;
		set
		{
			if(mode == value)
			{
				return;
			}
			if(IsOutputOn)
			{
				ErrorMessage="Tryb można zmienić dopiero po wyłączeniu wyjścia.";
				OnPropertyChanged();
				return;
			}

			mode=value;
			OnPropertyChanged();
			OnPropertyChanged(nameof(MaximumVoltageText));
			OnPropertyChanged(nameof(MaximumCurrentText));
			OnPropertyChanged(nameof(MaximumVoltageDescription));
			OnPropertyChanged(nameof(MaximumCurrentDescription));
			OnPropertyChanged(nameof(IsSymmetric));
			OnPropertyChanged(nameof(FirstSupplyLabel));
			OnPropertyChanged(nameof(SecondSupplyLabel));
			ValidateSetpoints();
			if(SetpointsAreValid)
			{
				controller?.SetMode(value);
				controller?.RequestVoltage(
					VoltageSetpoint.FromHundredths(voltageHundredths));
				controller?.RequestCurrent(
					CurrentSetpoint.FromThousandths(currentThousandths));
				UpdatePhysicalSetpointTexts();
			}
		}
	}

	public ObservableCollection<string> AvailableFirstPorts { get; }=[];
	public ObservableCollection<string> AvailableSecondPorts { get; }=[];

	public string? SelectedFirstPort
	{
		get => selectedFirstPort;
		set
		{
			if(updatingPorts)
			{
				return;
			}
			if(SetProperty(ref selectedFirstPort,value))
			{
				OnPropertyChanged(nameof(FirstPortName));
				RebuildPortLists();
			}
		}
	}

	public string? SelectedSecondPort
	{
		get => selectedSecondPort;
		set
		{
			if(updatingPorts)
			{
				return;
			}
			if(SetProperty(ref selectedSecondPort,value))
			{
				OnPropertyChanged(nameof(SecondPortName));
				RebuildPortLists();
			}
		}
	}

	public string FirstPortName
	{
		get => SelectedFirstPort ?? string.Empty;
		set => SelectedFirstPort=value;
	}

	public string SecondPortName
	{
		get => SelectedSecondPort ?? string.Empty;
		set => SelectedSecondPort=value;
	}

	public bool CanSelectPort => !IsConnected;

	public string VoltageText
	{
		get => voltageText;
		set => SetProperty(ref voltageText,value);
	}

	public string CurrentText
	{
		get => currentText;
		set => SetProperty(ref currentText,value);
	}

	public string MaximumVoltageText =>
		Mode == DualMode.Series ? "62,00" : "31,00";
	public string MaximumCurrentText =>
		Mode == DualMode.Parallel ? "10,200" : "5,100";
	public string MaximumVoltageDescription =>
		"Max napięcie zasilacza to "+
		(Mode == DualMode.Series ? "62V" : "31V");
	public string MaximumCurrentDescription =>
		"Max prąd zasilacza to "+
		(Mode == DualMode.Parallel ? "10,2A" : "5,1A");
	public bool IsSymmetric=>Mode == DualMode.Symmetric;
	public string FirstSupplyLabel=>IsSymmetric ? "Port 1 (+)" : "Zasilacz 1";
	public string SecondSupplyLabel=>IsSymmetric ? "Port 2 (-)" : "Zasilacz 2";

	public bool SetpointsAreValid
	{
		get => setpointsAreValid;
		private set => SetProperty(ref setpointsAreValid,value);
	}

	public bool IsConnected
	{
		get => isConnected;
		private set
		{
			if(SetProperty(ref isConnected,value))
			{
				OnPropertyChanged(nameof(ConnectionStatus));
				OnPropertyChanged(nameof(IsOffline));
				OnPropertyChanged(nameof(IsOff));
				OnPropertyChanged(nameof(IsOn));
				OnPropertyChanged(nameof(StatusText));
				OnPropertyChanged(nameof(CanSelectPort));
				ConnectCommand?.RaiseCanExecuteChanged();
				ToggleOutputCommand?.RaiseCanExecuteChanged();
				ConnectionStateChanged?.Invoke(this,value);
			}
		}
	}

	public bool IsOutputOn
	{
		get => isOutputOn;
		private set
		{
			if(SetProperty(ref isOutputOn,value))
			{
				OnPropertyChanged(nameof(OutputButtonText));
				OnPropertyChanged(nameof(IsOff));
				OnPropertyChanged(nameof(IsOn));
				OnPropertyChanged(nameof(StatusText));
				OutputStateChanged?.Invoke(this,value);
			}
		}
	}

	public string ConnectionStatus => IsConnected ? "Online" : "Offline";
	public string OutputButtonText => IsOutputOn ? "ON" : "OFF";
	public bool IsOffline => !IsConnected;
	public bool IsOff => IsConnected && !IsOutputOn;
	public bool IsOn => IsConnected && IsOutputOn;
	public string StatusText=>HasStatusError
		? "Stan zasilacza: BŁĄD - "+ErrorMessage
		: !IsConnected
			? "Stan zasilacza: OFFLINE"
			: IsOutputOn ? "Stan zasilacza: ON" : "Stan zasilacza: OFF";
	public bool HasStatusError=>!string.IsNullOrWhiteSpace(ErrorMessage);
	public ApplicationMode ApplicationMode => ApplicationMode.Dual;
	public string? PrimaryPort => SelectedFirstPort;
	public string? SecondaryPort => SelectedSecondPort;

	public ChartViewModel CreateChartViewModel(IFileDialogService fileDialog)
	{
		ArgumentNullException.ThrowIfNull(fileDialog);
		return new ChartViewModel(this,this,this,fileDialog);
	}

	public void UpdateAvailablePorts(IReadOnlyList<string> ports)
	{
		ArgumentNullException.ThrowIfNull(ports);
		portSnapshot.Clear();
		portSnapshot.AddRange(ports);
		if(IsConnected)
		{
			if(SelectedFirstPort is not null &&
				!portSnapshot.Contains(
					SelectedFirstPort,
					StringComparer.OrdinalIgnoreCase))
			{
				portSnapshot.Add(SelectedFirstPort);
			}
			if(SelectedSecondPort is not null &&
				!portSnapshot.Contains(
					SelectedSecondPort,
					StringComparer.OrdinalIgnoreCase))
			{
				portSnapshot.Add(SelectedSecondPort);
			}
		}
		RebuildPortLists();
	}

	public async ValueTask SetOutputAsync(
		bool enabled,
		CancellationToken cancellationToken)
	{
		await operationGate.WaitAsync(cancellationToken);
		try
		{
			DualPowerSupplyController current=controller ??
				throw new InvalidOperationException("Brak połączenia Dual.");
			DualOperationResult result=await current.SetOutputAsync(
				enabled,
				cancellationToken);
			if(result.IsSuccess)
			{
				IsOutputOn=result.RequestedEnabled;
				ErrorMessage=null;
			}
			else
			{
				IsOutputOn=false;
				ErrorMessage=BuildOperationError(result);
			}
		}
		finally
		{
			operationGate.Release();
		}
	}

	public async ValueTask ExportAsync(
		MeasurementExportKind kind,
		string path,
		CancellationToken cancellationToken)
	{
		DualMeasurement[] snapshot;
		lock(measurementGate)
		{
			snapshot=[.. measurements];
		}
		MeasurementLayout layout=Mode switch
		{
			DualMode.Series=>MeasurementLayout.Series,
			DualMode.Parallel=>MeasurementLayout.Parallel,
			DualMode.Symmetric=>MeasurementLayout.Symmetric,
			_=>throw new ArgumentOutOfRangeException()
		};
		await using FileStream stream=new(
			path,
			FileMode.Create,
			FileAccess.Write,
			FileShare.Read,
			4096,
			FileOptions.Asynchronous);
		await using StreamWriter text=new(stream);
		CsvMeasurementWriter writer=new(text);
		await writer.WriteHeaderAsync(layout,kind,cancellationToken);
		foreach(DualMeasurement measurement in snapshot)
		{
			await writer.WriteAsync(measurement,kind,cancellationToken);
		}
	}

	public string MeasuredVoltageText
	{
		get => measuredVoltageText;
		private set => SetProperty(ref measuredVoltageText,value);
	}

	public string MeasuredCurrentText
	{
		get => measuredCurrentText;
		private set => SetProperty(ref measuredCurrentText,value);
	}

	public string FirstMeasurementText
	{
		get => firstMeasurementText;
		private set => SetProperty(ref firstMeasurementText,value);
	}

	public string SecondMeasurementText
	{
		get => secondMeasurementText;
		private set => SetProperty(ref secondMeasurementText,value);
	}

	public string FirstSideVoltageText
	{
		get => firstSideVoltageText;
		private set => SetProperty(ref firstSideVoltageText,value);
	}

	public string FirstSideCurrentText
	{
		get => firstSideCurrentText;
		private set => SetProperty(ref firstSideCurrentText,value);
	}

	public string SecondSideVoltageText
	{
		get => secondSideVoltageText;
		private set => SetProperty(ref secondSideVoltageText,value);
	}

	public string SecondSideCurrentText
	{
		get => secondSideCurrentText;
		private set => SetProperty(ref secondSideCurrentText,value);
	}

	public string? ResistanceText
	{
		get => resistanceText;
		private set => SetProperty(ref resistanceText,value);
	}

	public bool IsResistanceVisible
	{
		get => isResistanceVisible;
		private set => SetProperty(ref isResistanceVisible,value);
	}

	public string FirstPhysicalSetpointText { get; private set; }=string.Empty;
	public string SecondPhysicalSetpointText { get; private set; }=string.Empty;

	public string? ErrorMessage
	{
		get => errorMessage;
		private set
		{
			if(SetProperty(ref errorMessage,value))
			{
				OnPropertyChanged(nameof(StatusText));
				OnPropertyChanged(nameof(HasStatusError));
			}
		}
	}

	public void SetVoltageFromHundredths(int value)
	{
		ApplyVoltage(value);
	}

	public async ValueTask CloseAsync(CancellationToken cancellationToken)
	{
		await DisconnectAsync(true,cancellationToken);
	}

	private async ValueTask DisconnectAsync(
		bool finalClose,
		CancellationToken cancellationToken)
	{
		if(closing)
		{
			return;
		}
		closing=true;
		await operationGate.WaitAsync(cancellationToken);
		try
		{
			if(controller is not null)
			{
				try
				{
					await controller.SetOutputAsync(false,cancellationToken);
				}
				catch(Exception exception)
				{
					ErrorMessage=exception.Message;
				}
				finally
				{
					DetachController(controller);
					await controller.DisposeAsync();
				}
			}
		}
		finally
		{
			controller=null;
			firstLease?.Dispose();
			secondLease?.Dispose();
			firstLease=null;
			secondLease=null;
			IsConnected=false;
			IsOutputOn=false;
			operationGate.Release();
			if(!finalClose)
			{
				closing=false;
				Interlocked.Exchange(ref faultCleanupScheduled,0);
			}
		}
	}

	private void InitializeCommands()
	{
		IncrementVoltageCommand=new RelayCommand(_=>ChangeVoltage(1));
		DecrementVoltageCommand=new RelayCommand(_=>ChangeVoltage(-1));
		IncrementCurrentCommand=new RelayCommand(_=>ChangeCurrent(1));
		DecrementCurrentCommand=new RelayCommand(_=>ChangeCurrent(-1));
		CommitVoltageCommand=new RelayCommand(_=>CommitVoltage());
		CommitCurrentCommand=new RelayCommand(_=>CommitCurrent());
		ToggleOutputCommand=new AsyncRelayCommand(
			ToggleOutputAsync,
			_=>IsConnected,
			exception=>ErrorMessage=exception.Message);
		ChangeModeCommand=new AsyncRelayCommand(
			ChangeModeAsync,
			parameter=>parameter is DualMode,
			exception=>ErrorMessage=exception.Message);
	}

	private async Task ConnectAsync(object? parameter)
	{
		if(leases is null || sessionFactory is null)
		{
			return;
		}
		string firstPort=SelectedFirstPort ??
			throw new InvalidOperationException("Wybierz pierwszy port COM.");
		string secondPort=SelectedSecondPort ??
			throw new InvalidOperationException("Wybierz drugi port COM.");
		if(string.Equals(firstPort,secondPort,StringComparison.OrdinalIgnoreCase))
		{
			ErrorMessage="Dla trybu Dual wybierz dwa różne porty COM.";
			return;
		}
		if(!leases.TryAcquire(firstPort,out PortLease? acquiredFirst))
		{
			ErrorMessage=$"Port {firstPort} jest już używany.";
			return;
		}
		if(!leases.TryAcquire(secondPort,out PortLease? acquiredSecond))
		{
			acquiredFirst.Dispose();
			ErrorMessage=$"Port {secondPort} jest już używany.";
			return;
		}

		SessionCreationResult firstResult;
		SessionCreationResult secondResult;
		try
		{
			Task<SessionCreationResult> firstTask=
				CreateSessionAsync(firstPort,sessionFactory);
			Task<SessionCreationResult> secondTask=
				CreateSessionAsync(secondPort,sessionFactory);
			await Task.WhenAll(firstTask,secondTask);
			firstResult=await firstTask;
			secondResult=await secondTask;
		}
		catch
		{
			acquiredFirst.Dispose();
			acquiredSecond.Dispose();
			throw;
		}

		if(firstResult.Session is null || secondResult.Session is null)
		{
			if(firstResult.Session is not null)
			{
				await firstResult.Session.DisposeAsync();
			}
			if(secondResult.Session is not null)
			{
				await secondResult.Session.DisposeAsync();
			}
			acquiredFirst.Dispose();
			acquiredSecond.Dispose();
			string failedPort=firstResult.Error is not null ? firstPort : secondPort;
			Exception error=firstResult.Error ?? secondResult.Error!;
			ErrorMessage=$"Nie można połączyć portu {failedPort}: {error.Message}";
			return;
		}

		firstLease=acquiredFirst;
		secondLease=acquiredSecond;
		controller=new DualPowerSupplyController(
			firstResult.Session,
			secondResult.Session,
			Mode);
		AttachController(controller);
		IsConnected=true;
		ErrorMessage=null;
		controller.RequestVoltage(VoltageSetpoint.FromHundredths(voltageHundredths));
		controller.RequestCurrent(CurrentSetpoint.FromThousandths(currentThousandths));
		UpdatePhysicalSetpointTexts();
	}

	private async Task ToggleConnectionAsync(object? parameter)
	{
		if(IsConnected)
		{
			await DisconnectAsync(false,CancellationToken.None);
			return;
		}
		await ConnectAsync(parameter);
	}

	private static async Task<SessionCreationResult> CreateSessionAsync(
		string port,
		ISingleSessionFactory factory)
	{
		try
		{
			IPowerSupplySession created=await factory.CreateAsync(
				port,
				CancellationToken.None);
			return new SessionCreationResult(created,null);
		}
		catch(Exception exception)
		{
			return new SessionCreationResult(null,exception);
		}
	}

	private async Task ToggleOutputAsync(object? parameter)
	{
		await SetOutputAsync(!IsOutputOn,CancellationToken.None);
	}

	private async Task ChangeModeAsync(object? parameter)
	{
		if(parameter is not DualMode target || target == Mode)
		{
			return;
		}
		await operationGate.WaitAsync();
		try
		{
			if(controller is not null)
			{
				DualOperationResult off=await controller.SetOutputAsync(
					false,
					CancellationToken.None);
				if(!off.IsSuccess)
				{
					ErrorMessage=BuildOperationError(off);
					OnPropertyChanged(nameof(Mode));
					return;
				}
				IsOutputOn=false;
			}
			Mode=target;
		}
		finally
		{
			operationGate.Release();
		}
	}

	private static string BuildOperationError(DualOperationResult result)
	{
		List<string> parts=[];
		if(result.FirstError is not null)
		{
			parts.Add("zasilacz 1: "+result.FirstError.Message);
		}
		if(result.SecondError is not null)
		{
			parts.Add("zasilacz 2: "+result.SecondError.Message);
		}
		return string.Join("; ",parts);
	}

	private void ChangeVoltage(int delta)
	{
		if(!TryParseScaled(VoltageText,100,GetMaximumVoltage(),out int value))
		{
			Invalidate("Nieprawidłowa nastawa napięcia.");
			return;
		}
		ApplyVoltage(Math.Clamp(value+delta,0,GetMaximumVoltage()));
	}

	private void ChangeCurrent(int delta)
	{
		if(!TryParseScaled(CurrentText,1000,GetMaximumCurrent(),out int value))
		{
			Invalidate("Nieprawidłowe ograniczenie prądu.");
			return;
		}
		ApplyCurrent(Math.Clamp(value+delta,0,GetMaximumCurrent()));
	}

	private void CommitVoltage()
	{
		if(!TryParseScaled(VoltageText,100,GetMaximumVoltage(),out int value))
		{
			Invalidate("Nieprawidłowa nastawa napięcia.");
			return;
		}
		ApplyVoltage(value);
	}

	private void CommitCurrent()
	{
		if(!TryParseScaled(CurrentText,1000,GetMaximumCurrent(),out int value))
		{
			Invalidate("Nieprawidłowe ograniczenie prądu.");
			return;
		}
		ApplyCurrent(value);
	}

	private void ApplyVoltage(int value)
	{
		if(value < 0 || value > GetMaximumVoltage())
		{
			Invalidate("Napięcie jest poza zakresem bieżącego trybu.");
			return;
		}
		VoltageSetpoint logical=VoltageSetpoint.FromHundredths(value);
		DualSetpointCalculator.Calculate(
			Mode,
			logical,
			CurrentSetpoint.FromThousandths(currentThousandths));
		voltageHundredths=value;
		VoltageText=(value/100m).ToString("0.00",PolishCulture);
		SetpointsAreValid=true;
		ErrorMessage=null;
		controller?.RequestVoltage(logical);
		UpdatePhysicalSetpointTexts();
	}

	private void ApplyCurrent(int value)
	{
		if(value < 0 || value > GetMaximumCurrent())
		{
			Invalidate("Prąd jest poza zakresem bieżącego trybu.");
			return;
		}
		CurrentSetpoint logical=CurrentSetpoint.FromThousandths(value);
		DualSetpointCalculator.Calculate(
			Mode,
			VoltageSetpoint.FromHundredths(voltageHundredths),
			logical);
		currentThousandths=value;
		CurrentText=(value/1000m).ToString("0.000",PolishCulture);
		SetpointsAreValid=true;
		ErrorMessage=null;
		controller?.RequestCurrent(logical);
		UpdatePhysicalSetpointTexts();
	}

	private void ValidateSetpoints()
	{
		SetpointsAreValid=
			voltageHundredths<=GetMaximumVoltage() &&
			currentThousandths<=GetMaximumCurrent();
		if(!SetpointsAreValid)
		{
			ErrorMessage="Nastawa przekracza zakres wybranego trybu.";
		}
	}

	private void UpdatePhysicalSetpointTexts()
	{
		if(!SetpointsAreValid)
		{
			return;
		}
		DualPhysicalSetpoints physical=DualSetpointCalculator.Calculate(
			Mode,
			VoltageSetpoint.FromHundredths(voltageHundredths),
			CurrentSetpoint.FromThousandths(currentThousandths));
		FirstPhysicalSetpointText=
			FormatVoltage(physical.FirstVoltage.Hundredths)+" / "+
			FormatCurrent(physical.FirstCurrent.Thousandths);
		SecondPhysicalSetpointText=
			FormatVoltage(physical.SecondVoltage.Hundredths)+" / "+
			FormatCurrent(physical.SecondCurrent.Thousandths);
		OnPropertyChanged(nameof(FirstPhysicalSetpointText));
		OnPropertyChanged(nameof(SecondPhysicalSetpointText));
	}

	private void AttachController(DualPowerSupplyController value)
	{
		value.DualSnapshotChanged+=OnDualSnapshotChanged;
		value.MeasurementReceived+=OnMeasurementReceived;
	}

	private void DetachController(DualPowerSupplyController value)
	{
		value.DualSnapshotChanged-=OnDualSnapshotChanged;
		value.MeasurementReceived-=OnMeasurementReceived;
	}

	private void OnDualSnapshotChanged(object? sender,DualControllerSnapshot snapshot)
	{
		DeviceCommunicationException? communicationError=
			snapshot.First.Error?.Exception as DeviceCommunicationException ??
			snapshot.Second.Error?.Exception as DeviceCommunicationException;
		if(communicationError is not null)
		{
			ScheduleFaultCleanup(communicationError.Message);
			return;
		}
		if(Volatile.Read(ref faultCleanupScheduled) != 0)
		{
			return;
		}
		Dispatch(()=>
		{
			if(snapshot.LastOutputOperation is { IsSuccess: false } operation)
			{
				ErrorMessage=BuildOperationError(operation);
			}
		});
	}

	private void ScheduleFaultCleanup(string message)
	{
		if(Interlocked.Exchange(ref faultCleanupScheduled,1) != 0)
		{
			return;
		}
		Dispatch(()=>ErrorMessage=message);
		_=Task.Run(async ()=>
		{
			await DisconnectAsync(false,CancellationToken.None);
		});
	}

	private void OnMeasurementReceived(object? sender,DualMeasurement value)
	{
		Dispatch(()=>
		{
			lock(measurementGate)
			{
				measurements.Add(value);
			}
			MeasuredVoltageText=FormatVoltage(value.VoltageHundredths);
			MeasuredCurrentText=FormatCurrent(value.CurrentThousandths);
			FirstMeasurementText=
				FormatVoltage(value.First.VoltageHundredths)+" / "+
				FormatCurrent(value.First.CurrentThousandths);
			SecondMeasurementText=
				FormatVoltage(value.Second.VoltageHundredths)+" / "+
				FormatCurrent(value.Second.CurrentThousandths);
			FirstSideVoltageText=FormatSignedVoltage(
				value.FirstSignedVoltageHundredths);
			FirstSideCurrentText=FormatSignedCurrent(
				value.FirstSignedCurrentThousandths);
			SecondSideVoltageText=FormatSignedVoltage(
				value.SecondSignedVoltageHundredths);
			SecondSideCurrentText=FormatSignedCurrent(
				value.SecondSignedCurrentThousandths);
			bool isCurrentLimited=
				Math.Abs(value.CurrentThousandths-currentThousandths)<=10;
			if(isCurrentLimited && ResistanceFormatter.TryFormat(
				value.VoltageHundredths,
				value.CurrentThousandths,
				out string? resistance))
			{
				ResistanceText=resistance;
				IsResistanceVisible=true;
			}
			else
			{
				ResistanceText=null;
				IsResistanceVisible=false;
			}
			TimeSpan elapsed=value.First.Elapsed >= value.Second.Elapsed
				? value.First.Elapsed
				: value.Second.Elapsed;
			ChartSampleReceived?.Invoke(
				this,
				new ChartSample(
					elapsed,
					value.VoltageHundredths,
					value.CurrentThousandths,
					isCurrentLimited,
					value.Mode == DualMode.Symmetric,
					value.FirstSignedVoltageHundredths,
					value.SecondSignedVoltageHundredths,
					value.FirstSignedCurrentThousandths,
					value.SecondSignedCurrentThousandths));
		});
	}

	private void Dispatch(Action action)
	{
		if(uiContext is null || SynchronizationContext.Current == uiContext)
		{
			action();
		}
		else
		{
			uiContext.Post(_=>action(),null);
		}
	}

	private void Invalidate(string message)
	{
		SetpointsAreValid=false;
		ErrorMessage=message;
	}

	private int GetMaximumVoltage()
	{
		return Mode == DualMode.Series ? 6200 : 3100;
	}

	private int GetMaximumCurrent()
	{
		return Mode == DualMode.Parallel ? 10200 : 5100;
	}

	private static string FormatVoltage(int value)
	{
		return (value/100m).ToString("0.00",PolishCulture)+" V";
	}

	private static string FormatCurrent(int value)
	{
		return (value/1000m).ToString("0.000",PolishCulture)+" A";
	}

	private static string FormatSignedVoltage(int value)
	{
		return (value/100m).ToString("+0.00;-0.00;+0.00",PolishCulture)+" V";
	}

	private static string FormatSignedCurrent(int value)
	{
		return (value/1000m).ToString("+0.000;-0.000;+0.000",PolishCulture)+" A";
	}

	private static bool TryParseScaled(
		string text,
		int scale,
		int maximum,
		out int value)
	{
		value=0;
		string normalized=text.Trim().Replace(',','.');
		if(!decimal.TryParse(
			normalized,
			NumberStyles.AllowDecimalPoint,
			CultureInfo.InvariantCulture,
			out decimal parsed) ||
			parsed < 0)
		{
			return false;
		}
		decimal scaled=parsed*scale;
		if(scaled != decimal.Truncate(scaled) || scaled > maximum)
		{
			return false;
		}
		value=(int)scaled;
		return true;
	}

	private sealed record SessionCreationResult(
		IPowerSupplySession? Session,
		Exception? Error);

	private bool CanConnect()
	{
		return !IsConnected &&
			SelectedFirstPort is not null &&
			SelectedSecondPort is not null &&
			!string.Equals(
				SelectedFirstPort,
				SelectedSecondPort,
				StringComparison.OrdinalIgnoreCase) &&
			portSnapshot.Contains(
				SelectedFirstPort,
				StringComparer.OrdinalIgnoreCase) &&
			portSnapshot.Contains(
				SelectedSecondPort,
				StringComparer.OrdinalIgnoreCase);
	}

	private void RestorePreferredPorts(string? first,string? second)
	{
		if(first is not null &&
			portSnapshot.Contains(first,StringComparer.OrdinalIgnoreCase))
		{
			selectedFirstPort=portSnapshot.First(port=>
				string.Equals(port,first,StringComparison.OrdinalIgnoreCase));
		}
		if(second is not null &&
			portSnapshot.Contains(second,StringComparer.OrdinalIgnoreCase) &&
			!string.Equals(
				selectedFirstPort,
				second,
				StringComparison.OrdinalIgnoreCase))
		{
			selectedSecondPort=portSnapshot.First(port=>
				string.Equals(port,second,StringComparison.OrdinalIgnoreCase));
		}
		RebuildPortLists();
	}

	private void RebuildPortLists()
	{
		updatingPorts=true;
		try
		{
			if(selectedFirstPort is null ||
				!portSnapshot.Contains(
					selectedFirstPort,
					StringComparer.OrdinalIgnoreCase))
			{
				selectedFirstPort=portSnapshot.FirstOrDefault(port=>
					!string.Equals(
						port,
						selectedSecondPort,
						StringComparison.OrdinalIgnoreCase));
			}
			if(selectedSecondPort is null ||
				!portSnapshot.Contains(
					selectedSecondPort,
					StringComparer.OrdinalIgnoreCase) ||
				string.Equals(
					selectedFirstPort,
					selectedSecondPort,
					StringComparison.OrdinalIgnoreCase))
			{
				selectedSecondPort=portSnapshot.FirstOrDefault(port=>
					!string.Equals(
						port,
						selectedFirstPort,
						StringComparison.OrdinalIgnoreCase));
			}

			SynchronizePorts(
				AvailableFirstPorts,
				portSnapshot.Where(port=>
				!string.Equals(
					port,
					selectedSecondPort,
					StringComparison.OrdinalIgnoreCase)));
			SynchronizePorts(
				AvailableSecondPorts,
				portSnapshot.Where(port=>
				!string.Equals(
					port,
					selectedFirstPort,
					StringComparison.OrdinalIgnoreCase)));
		}
		finally
		{
			updatingPorts=false;
		}
		OnPropertyChanged(nameof(SelectedFirstPort));
		OnPropertyChanged(nameof(SelectedSecondPort));
		OnPropertyChanged(nameof(FirstPortName));
		OnPropertyChanged(nameof(SecondPortName));
		ConnectCommand?.RaiseCanExecuteChanged();
	}

	private static void SynchronizePorts(
		ObservableCollection<string> target,
		IEnumerable<string> desiredPorts)
	{
		string[] desired=[.. desiredPorts];
		for(int index=0;index<desired.Length;index++)
		{
			if(index<target.Count && string.Equals(
				target[index],
				desired[index],
				StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			int existing=-1;
			for(int candidate=index+1;candidate<target.Count;candidate++)
			{
				if(string.Equals(
					target[candidate],
					desired[index],
					StringComparison.OrdinalIgnoreCase))
				{
					existing=candidate;
					break;
				}
			}

			if(existing >= 0)
			{
				target.Move(existing,index);
			}
			else
			{
				target.Insert(index,desired[index]);
			}
		}

		while(target.Count>desired.Length)
		{
			target.RemoveAt(target.Count-1);
		}
	}
}
