using Ka3005P.Core.Measurements;

namespace Ka3005P.Tests.Measurements;

public sealed class ResistanceFormatterTests
{
	[Theory]
	[InlineData(1,1000000,"10 µΩ")]
	[InlineData(1,10000,"1 mΩ")]
	[InlineData(1200,1000,"12 Ω")]
	[InlineData(1200,1,"12 kΩ")]
	public void TryFormat_SelectsReadableUnit(
		int voltageHundredths,
		int currentThousandths,
		string expected)
	{
		Assert.True(ResistanceFormatter.TryFormat(
			voltageHundredths,
			currentThousandths,
			out string? value));
		Assert.Equal(expected,value);
	}

	[Fact]
	public void TryFormat_RejectsZeroCurrent()
	{
		Assert.False(ResistanceFormatter.TryFormat(1200,0,out string? value));
		Assert.Null(value);
	}
}
