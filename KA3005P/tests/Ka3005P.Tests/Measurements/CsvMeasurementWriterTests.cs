using System.Globalization;
using Ka3005P.Core.Dual;
using Ka3005P.Core.Measurements;

namespace Ka3005P.Tests.Measurements;

public sealed class CsvMeasurementWriterTests
{
	[Fact]
	public async Task WriteSingleAsync_UsesSemicolonAndInvariantNumbers()
	{
		CultureInfo original=CultureInfo.CurrentCulture;
		CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("pl-PL");
		try
		{
			StringWriter output=new();
			CsvMeasurementWriter writer=new(output);
			await writer.WriteHeaderAsync(
				MeasurementLayout.Single,
				MeasurementExportKind.Voltage,
				CancellationToken.None);
			await writer.WriteAsync(
				new MeasurementSample(TimeSpan.FromMilliseconds(150),1234,123),
				MeasurementExportKind.Voltage,
				CancellationToken.None);

			Assert.Equal(
				"Sample;Time;Voltage;\n[-];[s];[V];\n0;0.150;12.34;\n",
				output.ToString());
		}
		finally
		{
			CultureInfo.CurrentCulture=original;
		}
	}

	[Fact]
	public async Task WriteDualAsync_UsesModeSpecificColumnsAndSignedSymmetricBranches()
	{
		StringWriter output=new();
		CsvMeasurementWriter writer=new(output);
		await writer.WriteHeaderAsync(
			MeasurementLayout.Symmetric,
			MeasurementExportKind.Voltage,
			CancellationToken.None);
		MeasurementSample first=new(TimeSpan.FromSeconds(1),1200,400);
		MeasurementSample second=new(TimeSpan.FromSeconds(1),1100,350);
		DualMeasurement measurement=DualMeasurement.Aggregate(
			DualMode.Symmetric,
			first,
			second);

		await writer.WriteAsync(
			measurement,
			MeasurementExportKind.Voltage,
			CancellationToken.None);

		Assert.Equal(
			"Sample;Time;Port 1 Voltage +;Port 2 Voltage -;\n[-];[s];[V];[V];\n0;1.000;12.00;-11.00;\n",
			output.ToString());
	}

	[Fact]
	public async Task WriteMissingAsync_LeavesMeasurementColumnsEmpty()
	{
		StringWriter output=new();
		CsvMeasurementWriter writer=new(output);

		await writer.WriteMissingAsync(
			TimeSpan.FromSeconds(2),
			MeasurementLayout.Series,
			MeasurementExportKind.Voltage,
			CancellationToken.None);

		Assert.Equal("0;2.000;;;;\n",output.ToString());
	}

	[Fact]
	public async Task WriteAsync_IncrementsSampleMarkerAcrossRows()
	{
		StringWriter output=new();
		CsvMeasurementWriter writer=new(output);

		await writer.WriteAsync(
			new MeasurementSample(TimeSpan.Zero,100,100),
			MeasurementExportKind.Current,
			CancellationToken.None);
		await writer.WriteAsync(
			new MeasurementSample(TimeSpan.FromMilliseconds(100),200,200),
			MeasurementExportKind.Current,
			CancellationToken.None);

		Assert.Equal("0;0.000;0.100;\n1;0.100;0.200;\n",output.ToString());
	}

	[Fact]
	public async Task WriteSingleBothAsync_ExportsVoltageAndCurrentInOneRow()
	{
		StringWriter output=new();
		CsvMeasurementWriter writer=new(output);
		await writer.WriteHeaderAsync(
			MeasurementLayout.Single,
			MeasurementExportKind.VoltageAndCurrent,
			CancellationToken.None);

		await writer.WriteAsync(
			new MeasurementSample(TimeSpan.FromMilliseconds(1250),1234,567),
			MeasurementExportKind.VoltageAndCurrent,
			CancellationToken.None);

		Assert.Equal(
			"Sample;Time;Voltage;Current;\n[-];[s];[V];[A];\n"+
			"0;1.250;12.34;0.567;\n",
			output.ToString());
	}

	[Fact]
	public async Task WriteDualBothAsync_ExportsModeSpecificVoltageAndCurrentColumns()
	{
		StringWriter output=new();
		CsvMeasurementWriter writer=new(output);
		await writer.WriteHeaderAsync(
			MeasurementLayout.Series,
			MeasurementExportKind.VoltageAndCurrent,
			CancellationToken.None);
		DualMeasurement measurement=DualMeasurement.Aggregate(
			DualMode.Series,
			new MeasurementSample(TimeSpan.FromSeconds(2),1200,400),
			new MeasurementSample(TimeSpan.FromSeconds(2),1100,350));

		await writer.WriteAsync(
			measurement,
			MeasurementExportKind.VoltageAndCurrent,
			CancellationToken.None);

		Assert.Equal(
			"Sample;Time;Voltage 1;Voltage 2;Voltage total;Current 1;Current 2;\n"+
			"[-];[s];[V];[V];[V];[A];[A];\n"+
			"0;2.000;12.00;11.00;23.00;0.400;0.350;\n",
			output.ToString());
	}

	[Fact]
	public async Task WriteMissingBothAsync_LeavesVoltageAndCurrentColumnsEmpty()
	{
		StringWriter output=new();
		CsvMeasurementWriter writer=new(output);

		await writer.WriteMissingAsync(
			TimeSpan.FromSeconds(3),
			MeasurementLayout.Single,
			MeasurementExportKind.VoltageAndCurrent,
			CancellationToken.None);

		Assert.Equal("0;3.000;;;\n",output.ToString());
	}
}
