using Ka3005P.App;

namespace Ka3005P.Tests;

public sealed class AppInformationTests
{
	[Fact]
	public void WindowTitle_UsesProjectNameAndVersion()
	{
		Assert.Equal("Korad KA3005P v0.2.0",AppInformation.GetWindowTitle());
		Assert.Equal("Korad KA3005P",AppInformation.DataDirectoryName);
	}

	[Fact]
	public void ApplicationAssembly_UsesIndependentExecutableName()
	{
		Assert.Equal(
			"Korad.KA3005P",
			typeof(AppInformation).Assembly.GetName().Name);
	}

	[Fact]
	public void ApplicationAssembly_DoesNotContainDemoTypes()
	{
		Assert.DoesNotContain(
			typeof(AppInformation).Assembly.GetTypes(),
			type=>type.Namespace?.StartsWith(
				"Ka3005P.App.Demo",
				StringComparison.Ordinal) == true);
	}

	[Fact]
	public void ApplicationAssembly_UsesSharedLabStationUiModule()
	{
		Assert.Contains(
			typeof(AppInformation).Assembly.GetReferencedAssemblies(),
			reference=>reference.Name == "LabStation.UI");
	}

	[Fact]
	public void AuthorText_ContainsRequestedIdentity()
	{
		Assert.Contains("Mateusz Skipor",AppInformation.AuthorText);
		Assert.Contains("mskiporsklep@op.pl",AppInformation.AuthorText);
		Assert.Contains("Inżynier technik elektroniki",AppInformation.AuthorText);
	}

	[Fact]
	public void EmbeddedLicense_ContainsPolyFormLicense()
	{
		string license=AppInformation.LoadLicenseText();

		Assert.Contains("PolyForm Noncommercial License 1.0.0",license);
	}
}
