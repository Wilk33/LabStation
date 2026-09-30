using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using LabStation.UI;

namespace Sdg1032X.App;

public static class ProductInformation
{
	private const string LicenseResourceName="Sdg1032X.LICENSE";

	public const string AuthorName="Mateusz Skipor";
	public const string AuthorProfession="Inżynier technik elektroniki";
	public const string AuthorEmail="mskiporsklep@op.pl";
	public const string DataDirectoryName="Siglent SDG1000X Control";
	public const string AuthorText=
		AuthorName+"\n"+AuthorProfession+"\n"+AuthorEmail;

	public static string Version
	{
		get
		{
			Version? version=typeof(ProductInformation).Assembly.GetName().Version;
			return version is null
				? "0.0.0"
				: $"{version.Major}.{version.Minor}.{version.Build}";
		}
	}

	public static string DisplayName=>"Siglent SDG1000X Control v"+Version;

	public static string GetWindowTitle()=>DisplayName;

	public static ApplicationPresentation Presentation { get; }=new(
		DisplayName,
		AuthorName,
		AuthorProfession,
		AuthorEmail,
		LoadLicenseText(),
		BitmapFrame.Create(new Uri(
			"pack://application:,,,/SDG1032X.Controller;component/Assets/sdg1062x.ico")));

	public static string LoadLicenseText()
	{
		Assembly assembly=typeof(ProductInformation).Assembly;
		using Stream stream=assembly.GetManifestResourceStream(LicenseResourceName) ??
			throw new InvalidOperationException("Nie znaleziono osadzonego tekstu licencji.");
		using StreamReader reader=new(stream);
		return reader.ReadToEnd();
	}
}
