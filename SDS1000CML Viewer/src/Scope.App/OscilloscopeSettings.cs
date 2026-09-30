using System.IO;
using System.Text.Json;

namespace Scope.App;

public sealed record OscilloscopeSettings(
	string Address,
	bool AutoConnect);

public interface IOscilloscopeSettingsStore
{
	OscilloscopeSettings Load();
	void Save(OscilloscopeSettings settings);
}

public sealed class JsonOscilloscopeSettingsStore : IOscilloscopeSettingsStore
{
	private readonly string path;
	private readonly OscilloscopeSettings defaults;

	public JsonOscilloscopeSettingsStore(
		string path,
		OscilloscopeSettings defaults)
	{
		this.path=path;
		this.defaults=defaults;
	}

	public OscilloscopeSettings Load()
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
			string address=root.TryGetProperty("address",out JsonElement addressValue)
				? addressValue.GetString() ?? ""
				: defaults.Address;
			bool autoConnect=defaults.AutoConnect;
			if(root.TryGetProperty("autoConnect",out JsonElement autoValue))
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

	public void Save(OscilloscopeSettings settings)
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
