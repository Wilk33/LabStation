using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using LabStation.UI;

namespace Sdm3000.App;

public static class AppInformation
{
	private const string LicenseResourceName="Sdm3000.LICENSE";

	public const string AuthorName="Mateusz Skipor";
	public const string AuthorProfession="Inżynier technik elektroniki";
	public const string AuthorEmail="mskiporsklep@op.pl";
	public const string DataDirectoryName="Siglent SDM3000 Viewer";

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

	public static string DisplayName=>"Siglent SDM3000 Control v"+Version;

	public static ApplicationPresentation Presentation { get; }=new(
		DisplayName,
		AuthorName,
		AuthorProfession,
		AuthorEmail,
		LoadLicenseText(),
		BitmapFrame.Create(new Uri(
			"pack://application:,,,/Siglent.SDM3000.Control;component/Assets/Siglent_SDM3055.ico")));

	public static string LoadLicenseText()
	{
		Assembly assembly=typeof(AppInformation).Assembly;
		using Stream stream=assembly.GetManifestResourceStream(LicenseResourceName) ??
			throw new InvalidOperationException("Nie znaleziono osadzonego tekstu licencji.");
		using StreamReader reader=new(stream);
		return reader.ReadToEnd();
	}
}
