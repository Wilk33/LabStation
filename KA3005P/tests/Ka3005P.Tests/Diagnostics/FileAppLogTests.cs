using Ka3005P.Core.Diagnostics;
using Ka3005P.Tests.Fakes;

namespace Ka3005P.Tests.Diagnostics;

public sealed class FileAppLogTests : IDisposable
{
	private readonly string directory=Path.Combine(
		Path.GetTempPath(),
		$"ka3005p-log-{Guid.NewGuid():N}");

	[Fact]
	public async Task WriteAsync_PreservesEveryRepeatedCommunicationFailure()
	{
		ManualTimeProvider time=new();
		string path=Path.Combine(directory,"app.log");
		FileAppLog log=new(path,time);

		await log.WriteAsync(
			AppLogLevel.Error,
			"Measurement",
			"COM8",
			new IOException("timeout"),
			CancellationToken.None);
		await log.WriteAsync(
			AppLogLevel.Error,
			"Measurement",
			"COM8",
			new IOException("timeout"),
			CancellationToken.None);

		string[] lines=await File.ReadAllLinesAsync(path,CancellationToken.None);
		Assert.Equal(2,lines.Length);
		Assert.All(lines,line=>
		{
			Assert.Contains("Measurement",line);
			Assert.Contains("COM8",line);
			Assert.Contains("IOException",line);
		});
	}

	public void Dispose()
	{
		if(Directory.Exists(directory))
		{
			Directory.Delete(directory,true);
		}
	}
}
