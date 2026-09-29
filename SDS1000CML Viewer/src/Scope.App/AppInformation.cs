using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using LabStation.UI;

namespace Scope.App;

public static class AppInformation
{
	private const string LicenseResourceName="Scope.App.LICENSE";

	public const string ProductName="Siglent SDS1000CML Viewer";
	public const string DataDirectoryName=ProductName;
	public const string AuthorName="Mateusz Skipor";
	public const string AuthorProfession="Inżynier technik elektroniki";
	public const string AuthorEmail="mskiporsklep@op.pl";

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

	public static string DisplayName=>ProductName+" v"+Version;

	public static ApplicationPresentation Presentation=>new(
		DisplayName,
		AuthorName,
		AuthorProfession,
		AuthorEmail,
		LoadLicenseText(),
		new BitmapImage(
			new Uri(
				"pack://application:,,,/Siglent.SDS1000CML.Viewer;component/Assets/siglent_sds1102cml+.ico",
				UriKind.Absolute)));

	public static string LoadLicenseText()
	{
		Assembly assembly=typeof(AppInformation).Assembly;
		using Stream stream=assembly.GetManifestResourceStream(LicenseResourceName) ??
			throw new InvalidOperationException("Nie znaleziono osadzonego tekstu licencji.");
		using StreamReader reader=new(stream);
		return reader.ReadToEnd();
	}
}
