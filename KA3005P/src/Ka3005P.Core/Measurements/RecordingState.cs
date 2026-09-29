namespace Ka3005P.Core.Measurements;

public enum RecordingState
{
	Running,
	Completing,
	Completed,
	Faulted
}

public sealed record RecordingFailure(
	DateTimeOffset OccurredAt,
	string Message,
	Exception? Exception);
