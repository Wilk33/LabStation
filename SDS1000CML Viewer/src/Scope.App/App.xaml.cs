using System.Windows;
using System.Windows.Threading;
using LabStation.UI;

namespace Scope.App;

public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs eventArgs)
	{
		base.OnStartup(eventArgs);
		SystemTheme.Initialize(Resources);
		DispatcherUnhandledException+=OnDispatcherUnhandledException;
		MainWindow window=new();
		MainWindow=window;
		window.Show();
	}

	private static void OnDispatcherUnhandledException(
		object sender,
		DispatcherUnhandledExceptionEventArgs eventArgs)
	{
		MessageBox.Show(
			eventArgs.Exception.Message,
			"Błąd aplikacji",
			MessageBoxButton.OK,
			MessageBoxImage.Error);
		eventArgs.Handled=true;
	}
}
