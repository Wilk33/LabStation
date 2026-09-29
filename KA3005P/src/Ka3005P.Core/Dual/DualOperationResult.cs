namespace Ka3005P.Core.Dual;

public sealed record DualOperationResult(
	Guid OperationId,
	bool RequestedEnabled,
	Exception? FirstError,
	Exception? SecondError,
	bool SafetyOffAttempted,
	Exception? FirstSafetyOffError,
	Exception? SecondSafetyOffError)
{
	public bool IsSuccess => FirstError is null && SecondError is null;
	public bool IsPartialFailure => (FirstError is null) != (SecondError is null);
}
