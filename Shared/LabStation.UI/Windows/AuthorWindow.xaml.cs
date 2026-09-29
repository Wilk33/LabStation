using System.Windows;

namespace LabStation.UI.Windows;

public partial class AuthorWindow : Window
{
	public AuthorWindow(ApplicationPresentation presentation)
	{
		ArgumentNullException.ThrowIfNull(presentation);
		InitializeComponent();
		DataContext=presentation;
		Title="Autor - "+presentation.DisplayName;
		Icon=presentation.Icon;
		SystemTheme.ApplyTo(this);
	}
}
