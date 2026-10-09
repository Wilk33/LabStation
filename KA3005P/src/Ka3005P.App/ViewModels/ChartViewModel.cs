using System.Collections.ObjectModel;
using System.Globalization;
using Ka3005P.App.Infrastructure;
using Ka3005P.App.Services;
using Ka3005P.Core.Measurements;

namespace Ka3005P.App.ViewModels;

public readonly record struct ChartPoint(
	TimeSpan Elapsed,
	double VoltageVolts,
	double CurrentAmperes,
	bool IsSymmetric=false,
	double FirstVoltageVolts=0,
	double SecondVoltageVolts=0,
	double FirstCurrentAmperes=0,
	double SecondCurrentAmperes=0);

public sealed class ChartViewModel : ObservableObject,IDisposable
{
	private const int MaximumPoints=600;
	private static readonly TimeSpan MaximumHistory=TimeSpan.FromMinutes(1);
	private const double CurrentAxisMargin=0.5;
	private const double VoltageAxisMargin=1;
	private static readonly CultureInfo PolishCulture=
		CultureInfo.GetCultureInfo("pl-PL");
	private readonly IOutputController outputController;
	private readonly IChartSampleSource? sampleSource;
	private readonly IMeasurementExporter? exporter;
	private readonly IFileDialogService? fileDialog;
	private bool isOutputOn;
	private bool isConnected;
	private bool showCurrent=true;
	private bool showVoltage;
	private bool isResistanceVisible;
	private double minimumY;
	private double maximumY=1;
	private double minimumVoltageY;
	private double maximumVoltageY=1;
	private string voltageText="0,00";
	private string currentText="0,000";
	private string firstVoltageText="0,00";
	private string secondVoltageText="0,00";
	private string firstCurrentText="0,000";
	private string secondCurrentText="0,000";
	private string? resistanceText;
	private string? errorMessage;
	private bool isSymmetric;
	private TimeSpan activeElapsed;
	private TimeSpan? previousSourceElapsed;
	private bool disposed;

	public ChartViewModel(IOutputController outputController)
		: this(outputController,null,null,null)
	{
	}

	public ChartViewModel(
		IOutputController outputController,
		IChartSampleSource? sampleSource,
		IMeasurementExporter? exporter,
		IFileDialogService? fileDialog)
	{
		ArgumentNullException.ThrowIfNull(outputController);
		this.outputController=outputController;
		this.sampleSource=sampleSource;
		this.exporter=exporter;
		this.fileDialog=fileDialog;
		isOutputOn=outputController.IsOutputOn;
		isConnected=outputController.IsConnected;
		outputController.OutputStateChanged+=OnOutputStateChanged;
		outputController.ConnectionStateChanged+=OnConnectionStateChanged;
		if(sampleSource is not null)
		{
			sampleSource.ChartSampleReceived+=OnChartSampleReceived;
		}
		ToggleOutputCommand=new AsyncRelayCommand(
			ToggleOutputAsync,
			_=>IsConnected,
			exception=>ErrorMessage=exception.Message);
		SaveVoltageCommand=new AsyncRelayCommand(
			_=>SaveAsync(MeasurementExportKind.Voltage),
			_=>exporter is not null && fileDialog is not null,
			exception=>ErrorMessage=exception.Message);
		SaveCurrentCommand=new AsyncRelayCommand(
			_=>SaveAsync(MeasurementExportKind.Current),
			_=>exporter is not null && fileDialog is not null,
			exception=>ErrorMessage=exception.Message);
		SaveVoltageAndCurrentCommand=new AsyncRelayCommand(
			_=>SaveAsync(MeasurementExportKind.VoltageAndCurrent),
			_=>exporter is not null && fileDialog is not null,
			exception=>ErrorMessage=exception.Message);
		ClearChartCommand=new RelayCommand(_=>ClearChart());
	}

	public ObservableCollection<ChartPoint> Points { get; }=[];
	public AsyncRelayCommand ToggleOutputCommand { get; }
	public AsyncRelayCommand SaveVoltageCommand { get; }
	public AsyncRelayCommand SaveCurrentCommand { get; }
	public AsyncRelayCommand SaveVoltageAndCurrentCommand { get; }
	public RelayCommand ClearChartCommand { get; }

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
				OnPropertyChanged(nameof(CanUseCursors));
				OnPropertyChanged(nameof(StatusText));
			}
		}
	}

	public bool IsConnected
	{
		get => isConnected;
		private set
		{
			if(SetProperty(ref isConnected,value))
			{
				OnPropertyChanged(nameof(IsOnline));
				OnPropertyChanged(nameof(IsOff));
				OnPropertyChanged(nameof(IsOn));
				OnPropertyChanged(nameof(CanUseCursors));
				OnPropertyChanged(nameof(StatusText));
				ToggleOutputCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public bool ShowCurrent
	{
		get => showCurrent;
		set => SetProperty(ref showCurrent,value);
	}

	public bool ShowVoltage
	{
		get => showVoltage;
		set => SetProperty(ref showVoltage,value);
	}

	public double MinimumY
	{
		get => minimumY;
		private set => SetProperty(ref minimumY,value);
	}

	public double MaximumY
	{
		get => maximumY;
		private set => SetProperty(ref maximumY,value);
	}

	public double MinimumVoltageY
	{
		get => minimumVoltageY;
		private set => SetProperty(ref minimumVoltageY,value);
	}

	public double MaximumVoltageY
	{
		get => maximumVoltageY;
		private set => SetProperty(ref maximumVoltageY,value);
	}

	public string VoltageText
	{
		get => voltageText;
		private set => SetProperty(ref voltageText,value);
	}

	public string CurrentText
	{
		get => currentText;
		private set => SetProperty(ref currentText,value);
	}

	public string FirstVoltageText
	{
		get=>firstVoltageText;
		private set=>SetProperty(ref firstVoltageText,value);
	}

	public string SecondVoltageText
	{
		get=>secondVoltageText;
		private set=>SetProperty(ref secondVoltageText,value);
	}

	public string FirstCurrentText
	{
		get=>firstCurrentText;
		private set=>SetProperty(ref firstCurrentText,value);
	}

	public string SecondCurrentText
	{
		get=>secondCurrentText;
		private set=>SetProperty(ref secondCurrentText,value);
	}

	public bool IsSymmetric
	{
		get=>isSymmetric;
		private set=>SetProperty(ref isSymmetric,value);
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

	public string OutputButtonText => IsOutputOn ? "ON" : "OFF";
	public bool IsOnline => IsConnected;
	public bool IsOff => IsConnected && !IsOutputOn;
	public bool IsOn => IsConnected && IsOutputOn;
	public bool CanUseCursors=>!IsOutputOn && Points.Count>0;
	public string StatusText=>HasStatusError
		? "Status: "+StatusErrorCode.FromMessage(ErrorMessage)
		: !IsConnected
			? "Status: OFFLINE"
			: IsOutputOn ? "Status: ON" : "Status: OFF";
	public bool HasStatusError=>!string.IsNullOrWhiteSpace(ErrorMessage);

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

	public void AddSample(TimeSpan elapsed,double value)
	{
		AddPoint(new ChartPoint(elapsed,0,value));
	}

	public void Dispose()
	{
		if(disposed)
		{
			return;
		}
		disposed=true;
		outputController.OutputStateChanged-=OnOutputStateChanged;
		outputController.ConnectionStateChanged-=OnConnectionStateChanged;
		if(sampleSource is not null)
		{
			sampleSource.ChartSampleReceived-=OnChartSampleReceived;
		}
	}

	private void AddPoint(ChartPoint point)
	{
		Points.Add(point);
		while(Points.Count>MaximumPoints ||
			Points.Count>0 && point.Elapsed-Points[0].Elapsed>MaximumHistory)
		{
			Points.RemoveAt(0);
		}
		OnPropertyChanged(nameof(CanUseCursors));
		RecalculateAxes();
	}

	private void ClearChart()
	{
		Points.Clear();
		activeElapsed=TimeSpan.Zero;
		previousSourceElapsed=null;
		OnPropertyChanged(nameof(CanUseCursors));
		RecalculateAxes();
	}

	private async Task ToggleOutputAsync(object? parameter)
	{
		await outputController.SetOutputAsync(
			!IsOutputOn,
			CancellationToken.None);
	}

	private async Task SaveAsync(MeasurementExportKind kind)
	{
		if(exporter is null || fileDialog is null)
		{
			return;
		}
		string name=kind switch
		{
			MeasurementExportKind.Voltage=>"napiecie.csv",
			MeasurementExportKind.Current=>"prad.csv",
			MeasurementExportKind.VoltageAndCurrent=>"napiecie-i-prad.csv",
			_=>throw new ArgumentOutOfRangeException(nameof(kind))
		};
		string? path=await fileDialog.ChooseSavePathAsync(
			name,
			CancellationToken.None);
		if(path is null)
		{
			return;
		}
		await exporter.ExportAsync(kind,path,CancellationToken.None);
		ErrorMessage=null;
	}

	private void RecalculateAxes()
	{
		if(Points.Count == 0)
		{
			MinimumY=0;
			MaximumY=1;
			MinimumVoltageY=0;
			MaximumVoltageY=1;
			return;
		}
		(double currentMinimum,double currentMaximum)=GetRange(
			Points.Select(point=>point.CurrentAmperes),
			CurrentAxisMargin);
		MinimumY=currentMinimum;
		MaximumY=currentMaximum;
		(double voltageMinimum,double voltageMaximum)=GetRange(
			Points.Select(point=>point.VoltageVolts),
			VoltageAxisMargin);
		MinimumVoltageY=voltageMinimum;
		MaximumVoltageY=voltageMaximum;
	}

	private static (double Minimum,double Maximum) GetRange(
		IEnumerable<double> values,
		double margin)
	{
		double minimum=values.Min();
		double maximum=values.Max();
		return (Math.Max(0,minimum-margin),maximum+margin);
	}

	private void OnOutputStateChanged(object? sender,bool enabled)
	{
		previousSourceElapsed=null;
		IsOutputOn=enabled;
	}

	private void OnConnectionStateChanged(object? sender,bool connected)
	{
		if(!connected)
		{
			previousSourceElapsed=null;
		}
		IsConnected=connected;
	}

	private void OnChartSampleReceived(object? sender,ChartSample sample)
	{
		if(!IsOutputOn)
		{
			return;
		}
		if(Points.Count>0 && Points[^1].IsSymmetric != sample.IsSymmetric)
		{
			ClearChart();
		}
		if(previousSourceElapsed is TimeSpan previous)
		{
			TimeSpan increment=sample.Elapsed-previous;
			if(increment>TimeSpan.Zero)
			{
				activeElapsed+=increment;
			}
		}
		previousSourceElapsed=sample.Elapsed;
		IsSymmetric=sample.IsSymmetric;
		AddPoint(new ChartPoint(
			activeElapsed,
			sample.VoltageHundredths/100d,
			sample.CurrentThousandths/1000d,
			sample.IsSymmetric,
			sample.FirstSignedVoltageHundredths/100d,
			sample.SecondSignedVoltageHundredths/100d,
			sample.FirstSignedCurrentThousandths/1000d,
			sample.SecondSignedCurrentThousandths/1000d));
		VoltageText=(sample.VoltageHundredths/100m)
			.ToString("0.00",PolishCulture);
		CurrentText=(sample.CurrentThousandths/1000m)
			.ToString("0.000",PolishCulture);
		FirstVoltageText=(sample.FirstSignedVoltageHundredths/100m)
			.ToString("0.00",PolishCulture);
		SecondVoltageText=(sample.SecondSignedVoltageHundredths/100m)
			.ToString("0.00",PolishCulture);
		FirstCurrentText=(sample.FirstSignedCurrentThousandths/1000m)
			.ToString("0.000",PolishCulture);
		SecondCurrentText=(sample.SecondSignedCurrentThousandths/1000m)
			.ToString("0.000",PolishCulture);
		if(sample.IsCurrentLimited && ResistanceFormatter.TryFormat(
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
	}
}
