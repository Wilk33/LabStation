using System.Globalization;
using System.Windows;
using LabStation.UI;

namespace Sdm3000.App;

public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs eventArgs)
	{
		CultureInfo culture=CultureInfo.GetCultureInfo("pl-PL");
		CultureInfo.DefaultThreadCurrentCulture=culture;
		CultureInfo.DefaultThreadCurrentUICulture=culture;
		base.OnStartup(eventArgs);
		SystemTheme.Initialize(Resources);
	}
}
