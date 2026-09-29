using System.Globalization;
using System.IO;
using System.Collections.ObjectModel;
using Ka3005P.App.Infrastructure;
using Ka3005P.App.Services;
using Ka3005P.Core.Configuration;
using Ka3005P.Core.Device;
using Ka3005P.Core.Measurements;
using Ka3005P.Core.Protocol;
using Ka3005P.Core.Sessions;

namespace Ka3005P.App.ViewModels;

public sealed class SingleSupplyViewModel : ObservableObject,ISupplyModeViewModel
{
	private static readonly CultureInfo PolishCulture=
		CultureInfo.GetCultureInfo("pl-PL");

	private readonly PortLeaseRegistry? leases;
	private readonly ISingleSessionFactory? sessionFactory;
	private readonly SynchronizationContext? uiContext=SynchronizationContext.Current;
	private readonly object measurementGate=new();
	private readonly SemaphoreSlim operationGate=new(1,1);
	private readonly List<MeasurementSample> measurements=[];
	private IPowerSupplySession? session;
	private PortLease? portLease;
	private string? selectedPort;
	private string voltageText="12,00";
	private string currentText="1,000";
	private string measuredVoltageText="0,00 V";
	private string measuredCurrentText="0,000 A";
	private string? resistanceText;
	private string? errorMessage;
	private int currentThousandths=1000;
	private bool isConnected;
	private bool isOutputOn;
	private bool isResistanceVisible;
	private bool hasValidationError;
	private bool closing;
	private int faultCleanupScheduled;

	public event EventHandler<bool>? OutputStateChanged;
	public event EventHandler<bool>? ConnectionStateChanged;
	public event EventHandler<ChartSample>? ChartSampleReceived;

	public SingleSupplyViewModel(IPowerSupplySession session)
	{
		ArgumentNullException.ThrowIfNull(session);
		this.session=session;
		isConnected=true;
		AttachSession(session);
		ConnectCommand=new AsyncRelayCommand(_=>Task.CompletedTask,_=>false);
		InitializeCommands();
	}

	public SingleSupplyViewModel(
		PortLeaseRegistry leases,
		ISingleSessionFactory sessionFactory,
		IReadOnlyList<string>? availablePorts=null,
		string? preferredPort=null)
	{
		ArgumentNullException.ThrowIfNull(leases);
		ArgumentNullException.ThrowIfNull(sessionFactory);
		this.leases=leases;
		this.sessionFactory=sessionFactory;
		UpdateAvailablePorts(availablePorts ?? ["COM5"]);
		if(preferredPort is not null &&
			AvailablePorts.Contains(preferredPort,StringComparer.OrdinalIgnoreCase))
		{
			SelectedPort=AvailablePorts.First(port=>
				string.Equals(port,preferredPort,StringComparison.OrdinalIgnoreCase));
		}
		ConnectCommand=new AsyncRelayCommand(
			ToggleConnectionAsync,
			_=>IsConnected ||
				(SelectedPort is not null &&
					AvailablePorts.Contains(
						SelectedPort,
						StringComparer.OrdinalIgnoreCase)),
			exception=>ErrorMessage=exception.Message);
		InitializeCommands();
	}

	public AsyncRelayCommand ConnectCommand { get; private set; }=null!;
	public RelayCommand IncrementVoltageCommand { get; private set; }=null!;
	public RelayCommand DecrementVoltageCommand { get; private set; }=null!;
	public RelayCommand IncrementCurrentCommand { get; private set; }=null!;
	public RelayCommand DecrementCurrentCommand { get; private set; }=null!;
	public RelayCommand CommitVoltageCommand { get; private set; }=null!;
	public RelayCommand CommitCurrentCommand { get; private set; }=null!;
	public AsyncRelayCommand ToggleOutputCommand { get; private set; }=null!;

	public ObservableCollection<string> AvailablePorts { get; }=[];

	public string? SelectedPort
	{
		get => selectedPort;
		set
		{
			if(SetProperty(ref selectedPort,value))
			{
				OnPropertyChanged(nameof(PortName));
				ConnectCommand?.RaiseCanExecuteChanged();
			}
		}
	}

	public string PortName
	{
		get => SelectedPort ?? string.Empty;
		set => SelectedPort=value;
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

	public string? ResistanceText
	{
		get => resistanceText;
		private set => SetProperty(ref resistanceText,value);
	}

	public string? ErrorMessage
	{
		get => errorMessage;
		private set => SetProperty(ref errorMessage,value);
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
				OutputStateChanged?.Invoke(this,value);
			}
		}
	}

	public bool IsResistanceVisible
	{
		get => isResistanceVisible;
		private set => SetProperty(ref isResistanceVisible,value);
	}

	public bool HasValidationError
	{
		get => hasValidationError;
		private set => SetProperty(ref hasValidationError,value);
	}

	public string ConnectionStatus => IsConnected ? "Online" : "Offline";
	public string OutputButtonText => IsOutputOn ? "ON" : "OFF";
	public bool IsOffline => !IsConnected;
	public bool IsOff => IsConnected && !IsOutputOn;
	public bool IsOn => IsConnected && IsOutputOn;
	public ApplicationMode ApplicationMode => ApplicationMode.Single;
	public string? PrimaryPort => SelectedPort;
	public string? SecondaryPort => null;

	public ChartViewModel CreateChartViewModel(IFileDialogService fileDialog)
	{
		ArgumentNullException.ThrowIfNull(fileDialog);
		return new ChartViewModel(this,this,this,fileDialog);
	}

	public void UpdateAvailablePorts(IReadOnlyList<string> ports)
	{
		ArgumentNullException.ThrowIfNull(ports);
		string? current=SelectedPort;
		AvailablePorts.Clear();
		foreach(string port in ports)
		{
			AvailablePorts.Add(port);
		}
		if(IsConnected && current is not null &&
			!AvailablePorts.Contains(current,StringComparer.OrdinalIgnoreCase))
		{
			AvailablePorts.Add(current);
		}
		if(current is not null &&
			AvailablePorts.Contains(current,StringComparer.OrdinalIgnoreCase))
		{
			SelectedPort=AvailablePorts.First(port=>
				string.Equals(port,current,StringComparison.OrdinalIgnoreCase));
		}
		else
		{
			SelectedPort=AvailablePorts.FirstOrDefault();
		}
		ConnectCommand?.RaiseCanExecuteChanged();
	}

	public async ValueTask SetOutputAsync(
		bool enabled,
		CancellationToken cancellationToken)
	{
		await operationGate.WaitAsync(cancellationToken);
		try
		{
			IPowerSupplySession current=session ??
				throw new InvalidOperationException("Brak połączenia z zasilaczem.");
			await current.SetOutputAsync(enabled,cancellationToken);
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
		MeasurementSample[] snapshot;
		lock(measurementGate)
		{
			snapshot=[.. measurements];
		}
		await using FileStream stream=new(
			path,
			FileMode.Create,
			FileAccess.Write,
			FileShare.Read,
			4096,
			FileOptions.Asynchronous);
		await using StreamWriter text=new(stream);
		CsvMeasurementWriter writer=new(text);
		await writer.WriteHeaderAsync(
			MeasurementLayout.Single,
			kind,
			cancellationToken);
		foreach(MeasurementSample sample in snapshot)
		{
			await writer.WriteAsync(sample,kind,cancellationToken);
		}
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
		IPowerSupplySession? current=session;
		try
		{
			if(current is not null)
			{
				try
				{
					await current.SetOutputAsync(false,cancellationToken);
				}
				catch(Exception exception)
				{
					ErrorMessage=exception.Message;
				}

				try
				{
					await current.StopAsync(cancellationToken);
				}
				finally
				{
					DetachSession(current);
					await current.DisposeAsync();
				}
			}
		}
		finally
		{
			session=null;
			portLease?.Dispose();
			portLease=null;
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
	}

	private async Task ConnectAsync(object? parameter)
	{
		if(leases is null || sessionFactory is null)
		{
			return;
		}
		string port=SelectedPort ??
			throw new InvalidOperationException("Wybierz aktywny port COM.");
		if(!leases.TryAcquire(port,out PortLease? acquired))
		{
			ErrorMessage=$"Port {port} jest już używany.";
			return;
		}

		try
		{
			IPowerSupplySession created=await sessionFactory.CreateAsync(
				acquired.PortName,
				CancellationToken.None);
			portLease=acquired;
			session=created;
			AttachSession(created);
			IsConnected=true;
			ErrorMessage=null;
			CommitVoltage();
			CommitCurrent();
		}
		catch
		{
			acquired.Dispose();
			throw;
		}
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

	private async Task ToggleOutputAsync(object? parameter)
	{
		await SetOutputAsync(!IsOutputOn,CancellationToken.None);
	}

	private void ChangeVoltage(int delta)
	{
		if(!TryParseScaled(VoltageText,100,3100,out int value))
		{
			SetValidationError("Napięcie musi być w zakresie 0,00-31,00 V.");
			return;
		}
		ApplyVoltage(Math.Clamp(value+delta,0,3100));
	}

	private void ChangeCurrent(int delta)
	{
		if(!TryParseScaled(CurrentText,1000,5100,out int value))
		{
			SetValidationError("Prąd musi być w zakresie 0,000-5,100 A.");
			return;
		}
		ApplyCurrent(Math.Clamp(value+delta,0,5100));
	}

	private void CommitVoltage()
	{
		if(!TryParseScaled(VoltageText,100,3100,out int value))
		{
			SetValidationError("Napięcie musi być w zakresie 0,00-31,00 V.");
			return;
		}
		ApplyVoltage(value);
	}

	private void CommitCurrent()
	{
		if(!TryParseScaled(CurrentText,1000,5100,out int value))
		{
			SetValidationError("Prąd musi być w zakresie 0,000-5,100 A.");
			return;
		}
		ApplyCurrent(value);
	}

	private void ApplyVoltage(int hundredths)
	{
		VoltageSetpoint value=VoltageSetpoint.FromHundredths(hundredths);
		VoltageText=(value.Hundredths/100m).ToString("0.00",PolishCulture);
		ClearValidationError();
		session?.RequestVoltage(value);
	}

	private void ApplyCurrent(int thousandths)
	{
		CurrentSetpoint value=CurrentSetpoint.FromThousandths(thousandths);
		currentThousandths=value.Thousandths;
		CurrentText=(value.Thousandths/1000m).ToString("0.000",PolishCulture);
		ClearValidationError();
		session?.RequestCurrent(value);
	}

	private static bool TryParseScaled(
		string text,
		int scale,
		int maximum,
		out int value)
	{
		value=0;
		if(string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
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

	private void AttachSession(IPowerSupplySession value)
	{
		value.SnapshotChanged+=OnSnapshotChanged;
		value.MeasurementReceived+=OnMeasurementReceived;
	}

	private void DetachSession(IPowerSupplySession value)
	{
		value.SnapshotChanged-=OnSnapshotChanged;
		value.MeasurementReceived-=OnMeasurementReceived;
	}

	private void OnSnapshotChanged(object? sender,SessionSnapshot snapshot)
	{
		if(snapshot.Error?.Exception is DeviceCommunicationException exception)
		{
			ScheduleFaultCleanup(exception.Message);
			return;
		}
		if(Volatile.Read(ref faultCleanupScheduled) != 0)
		{
			return;
		}
		Dispatch(()=>
		{
			IsOutputOn=snapshot.OutputState == OutputState.On;
			ErrorMessage=snapshot.Error?.Message;
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

	private void OnMeasurementReceived(object? sender,MeasurementSample sample)
	{
		Dispatch(()=>UpdateMeasurement(sample));
	}

	private void UpdateMeasurement(MeasurementSample sample)
	{
		lock(measurementGate)
		{
			measurements.Add(sample);
		}
		MeasuredVoltageText=(sample.VoltageHundredths/100m)
			.ToString("0.00",PolishCulture)+" V";
		MeasuredCurrentText=(sample.CurrentThousandths/1000m)
			.ToString("0.000",PolishCulture)+" A";
		bool isCurrentLimited=
			Math.Abs(sample.CurrentThousandths-currentThousandths)<=10;
		if(isCurrentLimited && ResistanceFormatter.TryFormat(
			sample.VoltageHundredths,
			sample.CurrentThousandths,
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
		ChartSampleReceived?.Invoke(
			this,
			new ChartSample(
				sample.Elapsed,
				sample.VoltageHundredths,
				sample.CurrentThousandths,
				isCurrentLimited));
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

	private void SetValidationError(string message)
	{
		HasValidationError=true;
		ErrorMessage=message;
	}

	private void ClearValidationError()
	{
		HasValidationError=false;
		ErrorMessage=null;
	}
}
