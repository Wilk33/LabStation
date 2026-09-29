using System.Windows;

namespace Ka3005P.App.Views;

public partial class AuthorWindow : Window
{
	public AuthorWindow()
	{
		InitializeComponent();
		SystemTheme.ApplyTo(this);
		Title="Autor - "+AppInformation.DisplayName;
	}
}
