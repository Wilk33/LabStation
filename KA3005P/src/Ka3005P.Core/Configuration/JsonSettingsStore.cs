using System.Text.Json;

namespace Ka3005P.Core.Configuration;

public interface ISettingsStore
{
	ValueTask<AppSettings> LoadAsync(CancellationToken cancellationToken);
	ValueTask SaveAsync(AppSettings settings,CancellationToken cancellationToken);
}

public sealed class JsonSettingsStore : ISettingsStore
{
	private static readonly JsonSerializerOptions Options=new()
	{
		WriteIndented=true
	};

	private readonly string path;

	public JsonSettingsStore(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		this.path=Path.GetFullPath(path);
	}

	public async ValueTask<AppSettings> LoadAsync(CancellationToken cancellationToken)
	{
		if(!File.Exists(path))
		{
			return new AppSettings();
		}

		try
		{
			await using FileStream input=new(
				path,
				FileMode.Open,
				FileAccess.Read,
				FileShare.Read);
			return await JsonSerializer.DeserializeAsync<AppSettings>(
				input,
				Options,
				cancellationToken).ConfigureAwait(false) ?? new AppSettings();
		}
		catch(JsonException)
		{
			File.Copy(path,path+".invalid",true);
			return new AppSettings();
		}
	}

	public async ValueTask SaveAsync(
		AppSettings settings,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(settings);
		string directory=Path.GetDirectoryName(path)
			?? throw new InvalidOperationException("Brak katalogu ustawień.");
		Directory.CreateDirectory(directory);
		string temporary=path+".tmp";
		await using(FileStream output=new(
			temporary,
			FileMode.Create,
			FileAccess.Write,
			FileShare.None,
			4096,
			FileOptions.Asynchronous))
		{
			await JsonSerializer.SerializeAsync(
				output,
				settings,
				Options,
				cancellationToken).ConfigureAwait(false);
			await output.FlushAsync(cancellationToken).ConfigureAwait(false);
			output.Flush(true);
		}
		File.Move(temporary,path,true);
	}
}
