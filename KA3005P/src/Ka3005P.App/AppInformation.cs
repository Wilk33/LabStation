using System.IO;
using System.Reflection;

namespace Ka3005P.App;

public static class AppInformation
{
	private const string LicenseResourceName="Ka3005P.LICENSE";

	public const string ProductName="Korad KA3005P";
	public const string DataDirectoryName=ProductName;
	public const string AuthorName="Mateusz Skipor";
	public const string AuthorProfession="Inżynier technik elektroniki";
	public const string AuthorEmail="mskiporsklep@op.pl";
	public const string AuthorText=
		AuthorName+"\n"+AuthorProfession+"\n"+AuthorEmail;

	public static string Version
	{
		get
		{
			System.Version? version=
				typeof(AppInformation).Assembly.GetName().Version;
			return version is null
				? "0.0.0"
				: $"{version.Major}.{version.Minor}.{version.Build}";
		}
	}

	public static string DisplayName => ProductName+" v"+Version;

	public static string GetWindowTitle()
	{
		return DisplayName;
	}

	public static string LoadLicenseText()
	{
		Assembly assembly=typeof(AppInformation).Assembly;
		using Stream stream=assembly.GetManifestResourceStream(LicenseResourceName) ??
			throw new InvalidOperationException("Nie znaleziono osadzonego tekstu licencji.");
		using StreamReader reader=new(stream);
		return reader.ReadToEnd();
	}
}
