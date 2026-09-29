using System.Windows;
using System.Windows.Controls;
using LabStation.UI.Windows;

namespace LabStation.UI.Controls;

public sealed class AboutMenuItem : MenuItem
{
	public static readonly DependencyProperty PresentationProperty=
		DependencyProperty.Register(
			nameof(Presentation),
			typeof(ApplicationPresentation),
			typeof(AboutMenuItem));

	public AboutMenuItem()
	{
		SetResourceReference(StyleProperty,typeof(MenuItem));
		Header="O aplikacji";
		MenuItem author=new()
		{
			Header="Autor"
		};
		MenuItem license=new()
		{
			Header="Licencja"
		};
		author.Click+=OpenAuthor;
		license.Click+=OpenLicense;
		Items.Add(author);
		Items.Add(license);
	}

	public ApplicationPresentation? Presentation
	{
		get=>(ApplicationPresentation?)GetValue(PresentationProperty);
		set=>SetValue(PresentationProperty,value);
	}

	private void OpenAuthor(object sender,RoutedEventArgs eventArgs)
	{
		if(Presentation is not ApplicationPresentation presentation)
		{
			return;
		}
		AuthorWindow window=new(presentation)
		{
			Owner=Window.GetWindow(this)
		};
		window.ShowDialog();
	}

	private void OpenLicense(object sender,RoutedEventArgs eventArgs)
	{
		if(Presentation is not ApplicationPresentation presentation)
		{
			return;
		}
		LicenseWindow window=new(presentation)
		{
			Owner=Window.GetWindow(this)
		};
		window.ShowDialog();
	}
}
