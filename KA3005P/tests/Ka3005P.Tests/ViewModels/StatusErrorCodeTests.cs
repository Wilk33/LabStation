using Ka3005P.App.Infrastructure;

namespace Ka3005P.Tests.ViewModels;

public sealed class StatusErrorCodeTests
{
	[Theory]
	[InlineData("Przekroczono limit czasu komunikacji z urządzeniem.","TOUT")]
	[InlineData("Port COM3 jest już używany.","PORT")]
	[InlineData("Urządzenie zwróciło nieprawidłową odpowiedź.","DATA")]
	[InlineData("Napięcie musi być w zakresie 0,00-31,00 V.","VAL")]
	[InlineData("Dla trybu Dual wybierz dwa różne porty COM.","CONF")]
	[InlineData("Nieznany błąd.","ERR")]
	public void FromMessage_ReturnsCompactCode(string message,string expected)
	{
		Assert.Equal(expected,StatusErrorCode.FromMessage(message));
	}
}
