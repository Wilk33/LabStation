using System.IO;
using System.Text.Json;

namespace Sdl1000X.App;

public sealed record ElectronicLoadSettings(
	string Address,
	bool AutoConnect);

public interface IElectronicLoadSettingsStore
{
	ElectronicLoadSettings Load();
	void Save(ElectronicLoadSettings settings);
}

public sealed class JsonElectronicLoadSettingsStore : IElectronicLoadSettingsStore
{
	private readonly string path;
	private readonly ElectronicLoadSettings defaults;

	public JsonElectronicLoadSettingsStore(
		string path,
		ElectronicLoadSettings defaults)
	{
		this.path=path;
		this.defaults=defaults;
	}

	public ElectronicLoadSettings Load()
	{
		try
		{
			if(!File.Exists(path))
			{
				return defaults;
			}
			using JsonDocument document=JsonDocument.Parse(File.ReadAllText(path));
			JsonElement root=document.RootElement;
			string address=root.TryGetProperty("address",out JsonElement addressValue)
				? addressValue.GetString() ?? ""
				: defaults.Address;
			bool autoConnect=root.TryGetProperty("autoConnect",out JsonElement autoValue)
				? autoValue.ValueKind switch
				{
					JsonValueKind.True=>true,
					JsonValueKind.False=>false,
					JsonValueKind.String=>bool.TryParse(autoValue.GetString(),out bool parsed) && parsed,
					_=>defaults.AutoConnect
				}
				: defaults.AutoConnect;
			return new(address,autoConnect);
		}
		catch(Exception)
		{
			return defaults;
		}
	}

	public void Save(ElectronicLoadSettings settings)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllText(
				path,
				JsonSerializer.Serialize(new Dictionary<string,object>
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
