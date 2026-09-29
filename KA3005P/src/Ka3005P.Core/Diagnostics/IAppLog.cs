namespace Ka3005P.Core.Diagnostics;

public enum AppLogLevel
{
	Information,
	Warning,
	Error
}

public interface IAppLog
{
	ValueTask WriteAsync(
		AppLogLevel level,
		string operation,
		string? port,
		Exception? exception,
		CancellationToken cancellationToken);
}
