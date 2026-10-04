using System.Windows;
using LabStation.UI;

namespace LabStation.App;

public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs eventArgs)
	{
		base.OnStartup(eventArgs);
		SystemTheme.Initialize(Resources);
		MainWindow window=new();
		MainWindow=window;
		window.Show();
	}
}
