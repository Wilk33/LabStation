namespace Ka3005P.Core.Sessions;

public sealed record SessionError(
	DateTimeOffset OccurredAt,
	string Message,
	Exception Exception);
