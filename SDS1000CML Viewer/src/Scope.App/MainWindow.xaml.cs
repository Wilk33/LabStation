using System.ComponentModel;
using System.Windows;
using Scope.App.Views;

namespace Scope.App;

public partial class MainWindow : Window
{
	private bool closeCompleted;

	public MainWindow()
	{
		InitializeComponent();
		Title=AppInformation.DisplayName;
		SystemTheme.ApplyTo(this);
	}

	private void OpenAuthorClick(object sender,RoutedEventArgs eventArgs)
	{
		AuthorWindow window=new()
		{
			Owner=this
		};
		window.ShowDialog();
	}

	private void OpenLicenseClick(object sender,RoutedEventArgs eventArgs)
	{
		LicenseWindow window=new()
		{
			Owner=this
		};
		window.ShowDialog();
	}

	protected override async void OnClosing(CancelEventArgs eventArgs)
	{
		if(closeCompleted)
		{
			base.OnClosing(eventArgs);
			return;
		}
		eventArgs.Cancel=true;
		await InstrumentPanel.DisposeAsync();
		closeCompleted=true;
		if(!Dispatcher.HasShutdownStarted)
		{
			await Dispatcher.InvokeAsync(Close);
		}
	}
}
