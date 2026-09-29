using System.IO;
using System.Windows;
using Ka3005P.App.Services;
using Ka3005P.App.ViewModels;
using Ka3005P.Core.Configuration;

namespace Ka3005P.App;

public partial class App
{
	private readonly PortLeaseRegistry portLeases=new();
	private readonly ISingleSessionFactory singleSessionFactory=
		new SerialSingleSessionFactory(TimeProvider.System);

	protected override async void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		SystemTheme.Initialize(Resources);
		string dataDirectory=Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			AppInformation.DataDirectoryName);
		ISettingsStore settings=new JsonSettingsStore(
			Path.Combine(dataDirectory,"settings.json"));
		ISerialPortCatalog catalog=new SystemSerialPortCatalog();
		SerialPortMonitor portMonitor=new(
			catalog,
			TimeSpan.FromSeconds(1));
		SupplyModeFactory modes=new(portLeases,singleSessionFactory);
		MainWindowViewModel viewModel=new(modes,portMonitor,settings);
		await viewModel.InitializeAsync(CancellationToken.None);
		MainWindow window=new()
		{
			DataContext=viewModel,
			Title=AppInformation.GetWindowTitle()
		};
		MainWindow=window;
		window.Show();
	}
}
