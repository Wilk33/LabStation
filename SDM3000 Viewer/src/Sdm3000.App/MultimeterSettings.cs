using System.IO;
using System.Text.Json;

namespace Sdm3000.App;

public sealed record MultimeterSettings(
	string Address,
	bool AutoConnect);

public interface IMultimeterSettingsStore
{
	MultimeterSettings Load();
	void Save(MultimeterSettings settings);
}

public sealed class JsonMultimeterSettingsStore : IMultimeterSettingsStore
{
	private readonly string path;
	private readonly MultimeterSettings defaults;

	public JsonMultimeterSettingsStore(
		string path,
		MultimeterSettings defaults)
	{
		this.path=path;
		this.defaults=defaults;
	}

	public MultimeterSettings Load()
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

	public void Save(MultimeterSettings settings)
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
