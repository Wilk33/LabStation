using Ka3005P.App.Services;

namespace Ka3005P.Tests.Services;

public sealed class SerialPortCatalogTests
{
	[Fact]
	public void Normalize_DeduplicatesAndNaturallySortsPortNames()
	{
		IReadOnlyList<string> result=SystemSerialPortCatalog.Normalize(
			["com10","COM2","COM2"," COM1 "]);

		Assert.Equal(["COM1","COM2","COM10"],result);
	}
}
