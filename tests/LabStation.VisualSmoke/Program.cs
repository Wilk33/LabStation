using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LabStation.App;
using LabStation.App.Korad;
using LabStation.UI;

namespace LabStation.VisualSmoke;

public static class Program
{
	[STAThread]
	public static int Main(string[] args)
	{
		if(args.Length is < 1 or > 2)
		{
			Console.Error.WriteLine(
				"Podaj ścieżkę PNG oraz opcjonalnie konfigurację Korada.");
			return 2;
		}
		KoradConfiguration configuration=args.Length == 2
			? Enum.Parse<KoradConfiguration>(args[1],true)
			: KoradConfiguration.OneSingle;
		string output=Path.GetFullPath(args[0]);
		string settingsPath=Path.ChangeExtension(output,".korad.json");
		Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");
		File.WriteAllText(
			settingsPath,
			JsonSerializer.Serialize(
				KoradWorkspaceSettings.Default with
				{
					Configuration=configuration
				}));
		Environment.SetEnvironmentVariable(
			"LABSTATION_DISABLE_AUTO_CONNECT",
			"1");
		Environment.SetEnvironmentVariable(
			"LABSTATION_KORAD_SETTINGS_PATH",
			settingsPath);
		Application application=new()
		{
			ShutdownMode=ShutdownMode.OnExplicitShutdown
		};
		application.Resources.MergedDictionaries.Add(new ResourceDictionary
		{
			Source=new Uri(
				"/LabStation.UI;component/Themes/LabStationTheme.xaml",
				UriKind.RelativeOrAbsolute)
		});
		SystemTheme.Initialize(application.Resources);
		MainWindow window=new()
		{
			WindowState=WindowState.Normal,
			WindowStartupLocation=WindowStartupLocation.Manual,
			Left=-20000,
			Top=-20000,
			Width=1920,
			Height=1040,
			ShowInTaskbar=false
		};
		window.Show();
		Pump(TimeSpan.FromSeconds(1));
		window.UpdateLayout();
		DpiScale dpi=VisualTreeHelper.GetDpi(window);
		int width=(int)Math.Ceiling(window.ActualWidth*dpi.DpiScaleX);
		int height=(int)Math.Ceiling(window.ActualHeight*dpi.DpiScaleY);
		RenderTargetBitmap bitmap=new(
			width,
			height,
			96*dpi.DpiScaleX,
			96*dpi.DpiScaleY,
			PixelFormats.Pbgra32);
		bitmap.Render(window);
		PngBitmapEncoder encoder=new();
		encoder.Frames.Add(BitmapFrame.Create(bitmap));
		using(FileStream stream=File.Create(output))
		{
			encoder.Save(stream);
		}
		window.Close();
		Pump(TimeSpan.FromSeconds(2));
		application.Shutdown();
		Console.WriteLine(output);
		return 0;
	}

	private static void Pump(TimeSpan duration)
	{
		DispatcherFrame frame=new();
		DispatcherTimer timer=new(
			DispatcherPriority.ApplicationIdle)
		{
			Interval=duration
		};
		timer.Tick+=(_,_)=>
		{
			timer.Stop();
			frame.Continue=false;
		};
		timer.Start();
		Dispatcher.PushFrame(frame);
	}
}
