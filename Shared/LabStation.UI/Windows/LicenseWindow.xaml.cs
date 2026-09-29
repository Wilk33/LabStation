using System.Windows;

namespace LabStation.UI.Windows;

public partial class LicenseWindow : Window
{
	public LicenseWindow(ApplicationPresentation presentation)
	{
		ArgumentNullException.ThrowIfNull(presentation);
		InitializeComponent();
		Title="Licencja - "+presentation.DisplayName;
		Icon=presentation.Icon;
		LicenseText.Text=presentation.LicenseText;
		SystemTheme.ApplyTo(this);
	}
}
