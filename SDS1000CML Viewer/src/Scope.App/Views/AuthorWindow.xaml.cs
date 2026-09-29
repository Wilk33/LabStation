using System.Windows;

namespace Scope.App.Views;

public partial class AuthorWindow : Window
{
	public AuthorWindow()
	{
		InitializeComponent();
		SystemTheme.ApplyTo(this);
		Title="Autor - "+AppInformation.DisplayName;
	}
}
