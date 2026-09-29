using System.Globalization;
using System.Text;
using Ka3005P.Core.Protocol;

namespace Ka3005P.Tests.Protocol;

public sealed class Ka3005PCommandCodecTests
{
	[Theory]
	[InlineData(0,"VSET1:00.00")]
	[InlineData(1200,"VSET1:12.00")]
	[InlineData(3100,"VSET1:31.00")]
	public void SetVoltage_FormatsHundredths(int raw,string expected)
	{
		VoltageSetpoint value=VoltageSetpoint.FromHundredths(raw);

		Assert.Equal(expected,Encoding.ASCII.GetString(Ka3005PCommandCodec.SetVoltage(value)));
	}

	[Theory]
	[InlineData(0,"ISET1:0.000")]
	[InlineData(1000,"ISET1:1.000")]
	[InlineData(5100,"ISET1:5.100")]
	public void SetCurrent_FormatsThousandths(int raw,string expected)
	{
		CurrentSetpoint value=CurrentSetpoint.FromThousandths(raw);

		Assert.Equal(expected,Encoding.ASCII.GetString(Ka3005PCommandCodec.SetCurrent(value)));
	}

	[Theory]
	[InlineData(false,"OUT0")]
	[InlineData(true,"OUT1")]
	public void SetOutput_FormatsState(bool enabled,string expected)
	{
		Assert.Equal(expected,Encoding.ASCII.GetString(Ka3005PCommandCodec.SetOutput(enabled)));
	}

	[Fact]
	public void ReadCommands_UseOnlyOutputMeasurements()
	{
		Assert.Equal("VOUT1?",Encoding.ASCII.GetString(Ka3005PCommandCodec.ReadVoltage()));
		Assert.Equal("IOUT1?",Encoding.ASCII.GetString(Ka3005PCommandCodec.ReadCurrent()));
	}

	[Fact]
	public void ParseVoltage_ParsesExactReply()
	{
		Assert.Equal(1200,Ka3005PCommandCodec.ParseVoltage("12.00"u8).Hundredths);
	}

	[Fact]
	public void ParseCurrent_ParsesExactReply()
	{
		Assert.Equal(1000,Ka3005PCommandCodec.ParseCurrent("1.000"u8).Thousandths);
	}

	[Theory]
	[InlineData("12.0")]
	[InlineData("12,00")]
	[InlineData("ab.cd")]
	public void ParseVoltage_RejectsMalformedReply(string reply)
	{
		Assert.Throws<ProtocolException>(()=>Ka3005PCommandCodec.ParseVoltage(Encoding.ASCII.GetBytes(reply)));
	}

	[Theory]
	[InlineData("1.00")]
	[InlineData("1,000")]
	[InlineData("a.bcd")]
	public void ParseCurrent_RejectsMalformedReply(string reply)
	{
		Assert.Throws<ProtocolException>(()=>Ka3005PCommandCodec.ParseCurrent(Encoding.ASCII.GetBytes(reply)));
	}

	[Fact]
	public void SetVoltage_RejectsValueAbovePhysicalLimit()
	{
		VoltageSetpoint logicalValue=VoltageSetpoint.FromHundredths(3101);

		Assert.Throws<ArgumentOutOfRangeException>(()=>Ka3005PCommandCodec.SetVoltage(logicalValue));
	}

	[Fact]
	public void SetCurrent_RejectsValueAbovePhysicalLimit()
	{
		CurrentSetpoint logicalValue=CurrentSetpoint.FromThousandths(5101);

		Assert.Throws<ArgumentOutOfRangeException>(()=>Ka3005PCommandCodec.SetCurrent(logicalValue));
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(6201)]
	public void VoltageSetpoint_RejectsValueOutsideLogicalRange(int raw)
	{
		Assert.Throws<ArgumentOutOfRangeException>(()=>VoltageSetpoint.FromHundredths(raw));
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(10201)]
	public void CurrentSetpoint_RejectsValueOutsideLogicalRange(int raw)
	{
		Assert.Throws<ArgumentOutOfRangeException>(()=>CurrentSetpoint.FromThousandths(raw));
	}

	[Fact]
	public void Formatting_IgnoresPolishCulture()
	{
		CultureInfo previous=CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("pl-PL");

			Assert.Equal("VSET1:12.34",Encoding.ASCII.GetString(
				Ka3005PCommandCodec.SetVoltage(VoltageSetpoint.FromHundredths(1234))));
			Assert.Equal("ISET1:1.234",Encoding.ASCII.GetString(
				Ka3005PCommandCodec.SetCurrent(CurrentSetpoint.FromThousandths(1234))));
		}
		finally
		{
			CultureInfo.CurrentCulture=previous;
		}
	}
}
