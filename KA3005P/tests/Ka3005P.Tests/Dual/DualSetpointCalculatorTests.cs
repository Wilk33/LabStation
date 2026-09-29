using Ka3005P.Core.Device;
using Ka3005P.Core.Dual;
using Ka3005P.Core.Measurements;
using Ka3005P.Core.Protocol;

namespace Ka3005P.Tests.Dual;

public sealed class DualSetpointCalculatorTests
{
	[Theory]
	[InlineData(0,0,0)]
	[InlineData(1,0,1)]
	[InlineData(2,1,1)]
	[InlineData(2501,1250,1251)]
	[InlineData(6200,3100,3100)]
	public void Series_SplitsVoltageWithoutLosingHundredth(
		int total,
		int first,
		int second)
	{
		DualPhysicalSetpoints result=DualSetpointCalculator.Calculate(
			DualMode.Series,
			VoltageSetpoint.FromHundredths(total),
			CurrentSetpoint.FromThousandths(1000));

		Assert.Equal(first,result.FirstVoltage.Hundredths);
		Assert.Equal(second,result.SecondVoltage.Hundredths);
		Assert.Equal(total,
			result.FirstVoltage.Hundredths+result.SecondVoltage.Hundredths);
		Assert.Equal(1000,result.FirstCurrent.Thousandths);
		Assert.Equal(1000,result.SecondCurrent.Thousandths);
	}

	[Theory]
	[InlineData(0,0,0)]
	[InlineData(1,0,1)]
	[InlineData(2,1,1)]
	[InlineData(5001,2500,2501)]
	[InlineData(10200,5100,5100)]
	public void Parallel_SplitsCurrentWithoutLosingThousandth(
		int total,
		int first,
		int second)
	{
		DualPhysicalSetpoints result=DualSetpointCalculator.Calculate(
			DualMode.Parallel,
			VoltageSetpoint.FromHundredths(1200),
			CurrentSetpoint.FromThousandths(total));

		Assert.Equal(1200,result.FirstVoltage.Hundredths);
		Assert.Equal(1200,result.SecondVoltage.Hundredths);
		Assert.Equal(first,result.FirstCurrent.Thousandths);
		Assert.Equal(second,result.SecondCurrent.Thousandths);
		Assert.Equal(total,
			result.FirstCurrent.Thousandths+result.SecondCurrent.Thousandths);
	}

	[Fact]
	public void Symmetric_UsesEqualPhysicalSetpoints()
	{
		DualPhysicalSetpoints result=DualSetpointCalculator.Calculate(
			DualMode.Symmetric,
			VoltageSetpoint.FromHundredths(1200),
			CurrentSetpoint.FromThousandths(1000));

		Assert.Equal(1200,result.FirstVoltage.Hundredths);
		Assert.Equal(1200,result.SecondVoltage.Hundredths);
		Assert.Equal(1000,result.FirstCurrent.Thousandths);
		Assert.Equal(1000,result.SecondCurrent.Thousandths);
	}

	[Theory]
	[InlineData(DualMode.Series,6200,5101)]
	[InlineData(DualMode.Parallel,3101,1000)]
	[InlineData(DualMode.Symmetric,3101,1000)]
	[InlineData(DualMode.Symmetric,1200,5101)]
	public void Calculate_RejectsValuesOutsideModeRange(
		DualMode mode,
		int voltage,
		int current)
	{
		Assert.Throws<ArgumentOutOfRangeException>(()=>
			DualSetpointCalculator.Calculate(
				mode,
				VoltageSetpoint.FromHundredths(voltage),
				CurrentSetpoint.FromThousandths(current)));
	}

	[Theory]
	[InlineData(DualMode.Series,3500,400,2300,200)]
	[InlineData(DualMode.Parallel,1200,1100,1100,700)]
	[InlineData(DualMode.Symmetric,1200,400,1100,400)]
	public void Aggregate_UsesModeSpecificLogicalValues(
		DualMode mode,
		int expectedVoltage,
		int expectedCurrent,
		int secondVoltage,
		int secondCurrent)
	{
		MeasurementSample first=new(
			DateTimeOffset.UnixEpoch,
			1,
			1200,
			400);
		MeasurementSample second=new(
			DateTimeOffset.UnixEpoch,
			2,
			secondVoltage,
			secondCurrent);

		DualMeasurement result=DualMeasurement.Aggregate(mode,first,second);

		Assert.Equal(expectedVoltage,result.VoltageHundredths);
		Assert.Equal(expectedCurrent,result.CurrentThousandths);
	}

	[Fact]
	public void Symmetric_AggregationPreservesSignedBranchesForExport()
	{
		MeasurementSample first=new(DateTimeOffset.UnixEpoch,1,1200,400);
		MeasurementSample second=new(DateTimeOffset.UnixEpoch,2,1100,350);

		DualMeasurement result=DualMeasurement.Aggregate(
			DualMode.Symmetric,
			first,
			second);

		Assert.Equal(-1200,result.FirstSignedVoltageHundredths);
		Assert.Equal(1100,result.SecondSignedVoltageHundredths);
		Assert.Equal(-400,result.FirstSignedCurrentThousandths);
		Assert.Equal(350,result.SecondSignedCurrentThousandths);
	}
}
