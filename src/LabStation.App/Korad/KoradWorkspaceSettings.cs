using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LabStation.App.Korad;

public enum KoradConfiguration
{
	OneSingle,
	TwoSingle,
	Dual
}

public sealed record KoradWorkspaceSettings(
	KoradConfiguration Configuration,
	string? FirstSinglePort,
	string? SecondSinglePort,
	string? DualFirstPort,
	string? DualSecondPort)
{
	public static KoradWorkspaceSettings Default { get; }=new(
		KoradConfiguration.OneSingle,
		null,
		null,
		null,
		null);
}

public interface IKoradWorkspaceSettingsStore
{
	ValueTask<KoradWorkspaceSettings> LoadAsync(
		CancellationToken cancellationToken);

	ValueTask SaveAsync(
		KoradWorkspaceSettings settings,
		CancellationToken cancellationToken);
}

public sealed class JsonKoradWorkspaceSettingsStore
	: IKoradWorkspaceSettingsStore
{
	private static readonly JsonSerializerOptions Options=new()
	{
		WriteIndented=true,
		Converters={new JsonStringEnumConverter()}
	};

	private readonly string path;

	public JsonKoradWorkspaceSettingsStore(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		this.path=Path.GetFullPath(path);
	}

	public async ValueTask<KoradWorkspaceSettings> LoadAsync(
		CancellationToken cancellationToken)
	{
		if(!File.Exists(path))
		{
			return KoradWorkspaceSettings.Default;
		}
		try
		{
			await using FileStream input=new(
				path,
				FileMode.Open,
				FileAccess.Read,
				FileShare.Read);
			KoradWorkspaceSettings? result=
				await JsonSerializer.DeserializeAsync<KoradWorkspaceSettings>(
					input,
					Options,
					cancellationToken);
			return result is not null &&
				Enum.IsDefined(result.Configuration)
				? result
				: KoradWorkspaceSettings.Default;
		}
		catch(JsonException)
		{
			File.Copy(path,path+".invalid",true);
			return KoradWorkspaceSettings.Default;
		}
	}

	public async ValueTask SaveAsync(
		KoradWorkspaceSettings settings,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(settings);
		string directory=Path.GetDirectoryName(path) ??
			throw new InvalidOperationException("Brak katalogu ustawień.");
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
				cancellationToken);
			await output.FlushAsync(cancellationToken);
			output.Flush(true);
		}
		File.Move(temporary,path,true);
	}
}

public sealed record KoradExportPaths(
	string PrimaryPath,
	string? SecondaryPath)
{
	public static KoradExportPaths ForConfiguration(
		string path,
		KoradConfiguration configuration)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		string full=Path.GetFullPath(path);
		if(configuration != KoradConfiguration.TwoSingle)
		{
			return new(full,null);
		}
		string directory=Path.GetDirectoryName(full) ?? string.Empty;
		string extension=Path.GetExtension(full);
		if(extension.Length == 0)
		{
			extension=".csv";
		}
		string name=Path.GetFileNameWithoutExtension(full);
		return new(
			Path.Combine(directory,name+"-P1"+extension),
			Path.Combine(directory,name+"-P2"+extension));
	}
}
