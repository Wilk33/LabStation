namespace Scope.App;

internal static class AppAssets

{

	public static Icon CreateIcon()

	{

		using Stream stream=Open("Scope.App.siglent_sds1102cml+.ico");
		using Icon icon=new(stream);
		return (Icon)icon.Clone();

	}

	public static string LicenseText

	{

		get

		{

			using Stream stream=Open("Scope.App.LICENSE");
			using StreamReader reader=new(stream);
			return reader.ReadToEnd().ReplaceLineEndings(Environment.NewLine);

		}

	}

	private static Stream Open(string name)

	{

		return typeof(AppAssets).Assembly.GetManifestResourceStream(name)
			?? throw new InvalidOperationException("Brak zasobu aplikacji: "+name);

	}


}
