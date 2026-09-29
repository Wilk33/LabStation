using System.Globalization;
using System.Text;

namespace Ka3005P.Core.Diagnostics;

public sealed class FileAppLog : IAppLog
{
	private readonly string path;
	private readonly TimeProvider timeProvider;
	private readonly SemaphoreSlim gate=new(1,1);

	public FileAppLog(string path,TimeProvider timeProvider)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		ArgumentNullException.ThrowIfNull(timeProvider);
		this.path=Path.GetFullPath(path);
		this.timeProvider=timeProvider;
	}

	public async ValueTask WriteAsync(
		AppLogLevel level,
		string operation,
		string? port,
		Exception? exception,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(operation);
		await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			string directory=Path.GetDirectoryName(path)
				?? throw new InvalidOperationException("Brak katalogu dziennika.");
			Directory.CreateDirectory(directory);
			string line=string.Join(
				'\t',
				timeProvider.GetUtcNow().ToString("O",CultureInfo.InvariantCulture),
				level.ToString(),
				operation,
				port ?? string.Empty,
				exception?.GetType().Name ?? string.Empty,
				exception?.Message.Replace('\t',' ') ?? string.Empty);
			await File.AppendAllTextAsync(
				path,
				line+Environment.NewLine,
				new UTF8Encoding(false),
				cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			gate.Release();
		}
	}
}
