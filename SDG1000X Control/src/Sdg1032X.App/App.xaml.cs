using System.Globalization;
using System.Windows;
using LabStation.UI;

namespace Sdg1032X.App;

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
