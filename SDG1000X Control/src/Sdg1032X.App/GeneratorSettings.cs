using System.IO;
using System.Text.Json;

namespace Sdg1032X.App;

public sealed record GeneratorSettings(
	string Address,
	bool AutoConnect);

public interface IGeneratorSettingsStore
{
	GeneratorSettings Load();
	void Save(GeneratorSettings settings);
}

public sealed class JsonGeneratorSettingsStore : IGeneratorSettingsStore
{
	private readonly string path;
	private readonly GeneratorSettings defaults;

	public JsonGeneratorSettingsStore(
		string path,
		GeneratorSettings defaults)
	{
		this.path=path;
		this.defaults=defaults;
	}

	public GeneratorSettings Load()
	{
		try
		{
			if(!File.Exists(path))
			{
				return defaults;
			}
			using JsonDocument document=JsonDocument.Parse(
				File.ReadAllText(path));
			JsonElement root=document.RootElement;
			string address=root.TryGetProperty(
				"address",
				out JsonElement addressValue)
				? addressValue.GetString() ?? ""
				: defaults.Address;
			bool autoConnect=defaults.AutoConnect;
			if(root.TryGetProperty(
				"autoConnect",
				out JsonElement autoValue))
			{
				autoConnect=autoValue.ValueKind switch
				{
					JsonValueKind.True=>true,
					JsonValueKind.False=>false,
					JsonValueKind.String=>bool.TryParse(
						autoValue.GetString(),
						out bool parsed) && parsed,
					_=>defaults.AutoConnect
				};
			}
			return new(address,autoConnect);
		}
		catch(Exception)
		{
			return defaults;
		}
	}

	public void Save(GeneratorSettings settings)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllText(
				path,
				JsonSerializer.Serialize(
					new Dictionary<string,object>
					{
						{"address",settings.Address},
						{"autoConnect",settings.AutoConnect}
					}));
		}
		catch(Exception)
		{
		}
	}
}
