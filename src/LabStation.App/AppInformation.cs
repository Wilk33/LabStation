using System.IO;
using System.Reflection;
using LabStation.UI;

namespace LabStation.App;

public static class AppInformation
{
	private const string LicenseResourceName="LabStation.LICENSE";

	public const string AuthorName="Mateusz Skipor";
	public const string AuthorProfession="Inżynier technik elektroniki";
	public const string AuthorEmail="mskiporsklep@op.pl";
	public const string DataDirectoryName="LabStation";

	public static string Version
	{
		get
		{
			Version? version=typeof(AppInformation).Assembly.GetName().Version;
			return version is null
				? "0.0.0"
				: $"{version.Major}.{version.Minor}.{version.Build}";
		}
	}

	public static string DisplayName=>"LabStation v"+Version;

	public static ApplicationPresentation Presentation { get; }=new(
		DisplayName,
		AuthorName,
		AuthorProfession,
		AuthorEmail,
		LoadLicenseText(),
		null);

	public static string LoadLicenseText()
	{
		Assembly assembly=typeof(AppInformation).Assembly;
		using Stream stream=assembly.GetManifestResourceStream(LicenseResourceName) ??
			throw new InvalidOperationException(
				"Nie znaleziono osadzonego tekstu licencji.");
		using StreamReader reader=new(stream);
		return reader.ReadToEnd();
	}
}
