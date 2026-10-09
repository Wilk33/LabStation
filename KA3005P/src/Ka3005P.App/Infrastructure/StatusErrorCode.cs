namespace Ka3005P.App.Infrastructure;

public static class StatusErrorCode
{
	public static string FromMessage(string? message)
	{
		if(string.IsNullOrWhiteSpace(message))
		{
			return "ERR";
		}
		if(Contains(message,"limit czasu") || Contains(message,"timeout"))
		{
			return "TOUT";
		}
		if(Contains(message,"nieprawidłow") ||
			Contains(message,"odpowiedź") ||
			Contains(message,"protok") ||
			Contains(message,"pełnej odpowiedzi"))
		{
			return "DATA";
		}
		if(Contains(message,"dual") ||
			Contains(message,"tryb") ||
			Contains(message,"zasilacz"))
		{
			return "CONF";
		}
		if(Contains(message,"port") ||
			Contains(message,"COM") ||
			Contains(message,"odłącz") ||
			Contains(message,"utrata"))
		{
			return "PORT";
		}
		if(Contains(message,"zakres") ||
			Contains(message,"napięcie musi") ||
			Contains(message,"prąd musi"))
		{
			return "VAL";
		}
		if(Contains(message,"komunikac"))
		{
			return "COM";
		}

		return "ERR";
	}

	private static bool Contains(string text,string value)
	{
		return text.Contains(value,StringComparison.OrdinalIgnoreCase);
	}
}
