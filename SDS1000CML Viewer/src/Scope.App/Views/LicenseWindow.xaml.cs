using System.Windows;

namespace Scope.App.Views;

public partial class LicenseWindow : Window
{
	public LicenseWindow()
	{
		InitializeComponent();
		SystemTheme.ApplyTo(this);
		Title="Licencja - "+AppInformation.DisplayName;
		LicenseText.Text=AppInformation.LoadLicenseText();
	}
}
