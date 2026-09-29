using System.Globalization;
using Ka3005P.Core.Dual;

namespace Ka3005P.Core.Measurements;

public enum MeasurementLayout
{
	Single,
	Series,
	Parallel,
	Symmetric
}

public enum MeasurementExportKind
{
	Voltage,
	Current,
	VoltageAndCurrent
}

public sealed class CsvMeasurementWriter
{
	private readonly TextWriter output;
	private long sampleIndex;

	public CsvMeasurementWriter(TextWriter output)
	{
		ArgumentNullException.ThrowIfNull(output);
		this.output=output;
	}

	public ValueTask WriteHeaderAsync(
		MeasurementLayout layout,
		MeasurementExportKind kind,
		CancellationToken cancellationToken)
	{
		(string labels,string units)=GetHeader(layout,kind);
		return WriteTextAsync(labels+"\n"+units+"\n",cancellationToken);
	}

	public ValueTask WriteAsync(
		MeasurementSample sample,
		MeasurementExportKind kind,
		CancellationToken cancellationToken)
	{
		string value=kind switch
		{
			MeasurementExportKind.Voltage=>
				FormatVoltage(sample.VoltageHundredths),
			MeasurementExportKind.Current=>
				FormatCurrent(sample.CurrentThousandths),
			MeasurementExportKind.VoltageAndCurrent=>
				FormatVoltage(sample.VoltageHundredths)+";"+
				FormatCurrent(sample.CurrentThousandths),
			_=>throw new ArgumentOutOfRangeException(nameof(kind))
		};
		return WriteTextAsync(
			$"{NextSampleIndex()};{FormatTime(sample.Elapsed)};{value};\n",
			cancellationToken);
	}

	public ValueTask WriteAsync(
		DualMeasurement measurement,
		MeasurementExportKind kind,
		CancellationToken cancellationToken)
	{
		string line=kind switch
		{
			MeasurementExportKind.Voltage=>FormatDualVoltage(measurement),
			MeasurementExportKind.Current=>FormatDualCurrent(measurement),
			MeasurementExportKind.VoltageAndCurrent=>
				FormatDualVoltageAndCurrent(measurement),
			_=>throw new ArgumentOutOfRangeException(nameof(kind))
		};
		return WriteTextAsync(line,cancellationToken);
	}

	public ValueTask WriteMissingAsync(
		TimeSpan elapsed,
		MeasurementLayout layout,
		MeasurementExportKind kind,
		CancellationToken cancellationToken)
	{
		int valueCount=GetValueCount(layout,kind);
		return WriteTextAsync(
			NextSampleIndex()+";"+FormatTime(elapsed)+";"+
			new string(';',valueCount)+"\n",
			cancellationToken);
	}

	private static (string Labels,string Units) GetHeader(
		MeasurementLayout layout,
		MeasurementExportKind kind)
	{
		return (layout,kind) switch
		{
			(MeasurementLayout.Single,MeasurementExportKind.Voltage)=>
				("Sample;Time;Voltage;","[-];[s];[V];"),
			(MeasurementLayout.Single,MeasurementExportKind.Current)=>
				("Sample;Time;Current;","[-];[s];[A];"),
			(MeasurementLayout.Single,MeasurementExportKind.VoltageAndCurrent)=>
				("Sample;Time;Voltage;Current;","[-];[s];[V];[A];"),
			(MeasurementLayout.Series,MeasurementExportKind.Voltage)=>
				("Sample;Time;Voltage 1;Voltage 2;Voltage total;",
				"[-];[s];[V];[V];[V];"),
			(MeasurementLayout.Series,MeasurementExportKind.Current)=>
				("Sample;Time;Current 1;Current 2;","[-];[s];[A];[A];"),
			(MeasurementLayout.Series,MeasurementExportKind.VoltageAndCurrent)=>
				("Sample;Time;Voltage 1;Voltage 2;Voltage total;"+
				"Current 1;Current 2;",
				"[-];[s];[V];[V];[V];[A];[A];"),
			(MeasurementLayout.Parallel,MeasurementExportKind.Voltage)=>
				("Sample;Time;Voltage 1;Voltage 2;","[-];[s];[V];[V];"),
			(MeasurementLayout.Parallel,MeasurementExportKind.Current)=>
				("Sample;Time;Current 1;Current 2;Current total;",
				"[-];[s];[A];[A];[A];"),
			(MeasurementLayout.Parallel,MeasurementExportKind.VoltageAndCurrent)=>
				("Sample;Time;Voltage 1;Voltage 2;Current 1;Current 2;"+
				"Current total;",
				"[-];[s];[V];[V];[A];[A];[A];"),
			(MeasurementLayout.Symmetric,MeasurementExportKind.Voltage)=>
				("Sample;Time;Voltage -;Voltage +;","[-];[s];[V];[V];"),
			(MeasurementLayout.Symmetric,MeasurementExportKind.Current)=>
				("Sample;Time;Current -;Current +;","[-];[s];[A];[A];"),
			(MeasurementLayout.Symmetric,MeasurementExportKind.VoltageAndCurrent)=>
				("Sample;Time;Voltage -;Voltage +;Current -;Current +;",
				"[-];[s];[V];[V];[A];[A];"),
			_=>throw new ArgumentOutOfRangeException(nameof(layout))
		};
	}

	private string FormatDualVoltage(DualMeasurement measurement)
	{
		string prefix=NextSampleIndex()+";"+FormatTime(MaxElapsed(measurement));
		return measurement.Mode switch
		{
			DualMode.Series=>
				$"{prefix};{FormatVoltage(measurement.First.VoltageHundredths)};"+
				$"{FormatVoltage(measurement.Second.VoltageHundredths)};"+
				$"{FormatVoltage(measurement.VoltageHundredths)};\n",
			DualMode.Parallel=>
				$"{prefix};{FormatVoltage(measurement.First.VoltageHundredths)};"+
				$"{FormatVoltage(measurement.Second.VoltageHundredths)};\n",
			DualMode.Symmetric=>
				$"{prefix};{FormatVoltage(measurement.FirstSignedVoltageHundredths)};"+
				$"{FormatVoltage(measurement.SecondSignedVoltageHundredths)};\n",
			_=>throw new ArgumentOutOfRangeException(nameof(measurement))
		};
	}

	private string FormatDualCurrent(DualMeasurement measurement)
	{
		string prefix=NextSampleIndex()+";"+FormatTime(MaxElapsed(measurement));
		return measurement.Mode switch
		{
			DualMode.Series=>
				$"{prefix};{FormatCurrent(measurement.First.CurrentThousandths)};"+
				$"{FormatCurrent(measurement.Second.CurrentThousandths)};\n",
			DualMode.Parallel=>
				$"{prefix};{FormatCurrent(measurement.First.CurrentThousandths)};"+
				$"{FormatCurrent(measurement.Second.CurrentThousandths)};"+
				$"{FormatCurrent(measurement.CurrentThousandths)};\n",
			DualMode.Symmetric=>
				$"{prefix};{FormatCurrent(measurement.FirstSignedCurrentThousandths)};"+
				$"{FormatCurrent(measurement.SecondSignedCurrentThousandths)};\n",
			_=>throw new ArgumentOutOfRangeException(nameof(measurement))
		};
	}

	private string FormatDualVoltageAndCurrent(DualMeasurement measurement)
	{
		string prefix=NextSampleIndex()+";"+FormatTime(MaxElapsed(measurement));
		return measurement.Mode switch
		{
			DualMode.Series=>
				$"{prefix};{FormatVoltage(measurement.First.VoltageHundredths)};"+
				$"{FormatVoltage(measurement.Second.VoltageHundredths)};"+
				$"{FormatVoltage(measurement.VoltageHundredths)};"+
				$"{FormatCurrent(measurement.First.CurrentThousandths)};"+
				$"{FormatCurrent(measurement.Second.CurrentThousandths)};\n",
			DualMode.Parallel=>
				$"{prefix};{FormatVoltage(measurement.First.VoltageHundredths)};"+
				$"{FormatVoltage(measurement.Second.VoltageHundredths)};"+
				$"{FormatCurrent(measurement.First.CurrentThousandths)};"+
				$"{FormatCurrent(measurement.Second.CurrentThousandths)};"+
				$"{FormatCurrent(measurement.CurrentThousandths)};\n",
			DualMode.Symmetric=>
				$"{prefix};{FormatVoltage(measurement.FirstSignedVoltageHundredths)};"+
				$"{FormatVoltage(measurement.SecondSignedVoltageHundredths)};"+
				$"{FormatCurrent(measurement.FirstSignedCurrentThousandths)};"+
				$"{FormatCurrent(measurement.SecondSignedCurrentThousandths)};\n",
			_=>throw new ArgumentOutOfRangeException(nameof(measurement))
		};
	}

	private static int GetValueCount(
		MeasurementLayout layout,
		MeasurementExportKind kind)
	{
		return (layout,kind) switch
		{
			(MeasurementLayout.Single,MeasurementExportKind.VoltageAndCurrent)=>2,
			(MeasurementLayout.Single,_)=>1,
			(MeasurementLayout.Series,MeasurementExportKind.Voltage)=>3,
			(MeasurementLayout.Series,MeasurementExportKind.Current)=>2,
			(MeasurementLayout.Series,MeasurementExportKind.VoltageAndCurrent)=>5,
			(MeasurementLayout.Parallel,MeasurementExportKind.Voltage)=>2,
			(MeasurementLayout.Parallel,MeasurementExportKind.Current)=>3,
			(MeasurementLayout.Parallel,MeasurementExportKind.VoltageAndCurrent)=>5,
			(MeasurementLayout.Symmetric,MeasurementExportKind.VoltageAndCurrent)=>4,
			(MeasurementLayout.Symmetric,_)=>2,
			_=>throw new ArgumentOutOfRangeException(nameof(layout))
		};
	}

	private static TimeSpan MaxElapsed(DualMeasurement measurement)
	{
		return measurement.First.Elapsed >= measurement.Second.Elapsed
			? measurement.First.Elapsed
			: measurement.Second.Elapsed;
	}

	private static string FormatTime(TimeSpan elapsed)
	{
		return elapsed.TotalSeconds.ToString("0.000",CultureInfo.InvariantCulture);
	}

	private static string FormatVoltage(int hundredths)
	{
		return (hundredths/100m).ToString("0.00",CultureInfo.InvariantCulture);
	}

	private static string FormatCurrent(int thousandths)
	{
		return (thousandths/1000m).ToString("0.000",CultureInfo.InvariantCulture);
	}

	private long NextSampleIndex()
	{
		return sampleIndex++;
	}

	private async ValueTask WriteTextAsync(
		string value,
		CancellationToken cancellationToken)
	{
		await output.WriteAsync(value.AsMemory(),cancellationToken).ConfigureAwait(false);
	}
}
