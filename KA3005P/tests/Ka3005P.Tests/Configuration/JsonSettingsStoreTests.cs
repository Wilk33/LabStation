using Ka3005P.Core.Configuration;

namespace Ka3005P.Tests.Configuration;

public sealed class JsonSettingsStoreTests : IDisposable
{
	private readonly string directory=Path.Combine(
		Path.GetTempPath(),
		$"ka3005p-settings-{Guid.NewGuid():N}");
	private readonly string path;

	public JsonSettingsStoreTests()
	{
		path=Path.Combine(directory,"settings.json");
	}

	[Fact]
	public async Task SaveAndLoadAsync_RoundTripsSettings()
	{
		JsonSettingsStore store=new(path);
		AppSettings expected=new()
		{
			SinglePort="COM5",
			DualFirstPort="COM6",
			DualSecondPort="COM7",
			ExportDirectory="D:\\Pomiary"
		};

		await store.SaveAsync(expected,CancellationToken.None);
		AppSettings actual=await store.LoadAsync(CancellationToken.None);

		Assert.Equal(expected,actual);
		Assert.False(File.Exists(path+".tmp"));
	}

	[Fact]
	public async Task LoadAsync_CorruptJsonReturnsDefaultsAndPreservesInvalidFile()
	{
		Directory.CreateDirectory(directory);
		await File.WriteAllTextAsync(path,"{ broken",CancellationToken.None);
		JsonSettingsStore store=new(path);

		AppSettings actual=await store.LoadAsync(CancellationToken.None);

		Assert.Equal(new AppSettings(),actual);
		Assert.True(File.Exists(path+".invalid"));
	}

	public void Dispose()
	{
		if(Directory.Exists(directory))
		{
			Directory.Delete(directory,true);
		}
	}
}
