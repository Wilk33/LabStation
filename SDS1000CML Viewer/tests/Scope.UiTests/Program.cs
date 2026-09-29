using System.Reflection;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Scope.App;
using Scope.Core;
using LabStation.Instruments.Transport;
using LabStation.UI.Windows;

internal static class Program
{
	[STAThread]
	private static int Main(string[] args)
	{
		App application=new();
		application.InitializeComponent();
		SynchronizationContext.SetSynchronizationContext(
			new System.Windows.Threading.DispatcherSynchronizationContext(
				application.Dispatcher));
		MainWindow window=new()
		{
			ShowInTaskbar=false,
			WindowStyle=WindowStyle.None,
			Left=-10000,
			Top=-10000
		};
		window.Show();
		Pump();

		AssertApplicationIdentity(window);
		AssertReusablePanel(window);
		AssertConnectionControls(window);
		AssertConnectionAndCursorControlsShareOneRow(window);
		AssertMenuPopupHasNoFrame(window);
		AssertSharedUiLibrary();
		AssertSharedAboutMenuComponent(window);
		AssertSharedControlDefaults();
		AssertCommonStyle(window);
		AssertAboutWindows(window);
		AssertPlotAndChannels(window);
		AssertOnlineDisconnectAvailableDuringPreview(window);
		AssertLayoutAndCapture(window);

		OscilloscopeView panel=Find<OscilloscopeView>(window);
		panel.DisposeAsync().AsTask().GetAwaiter().GetResult();
		window.Close();
		Pump();
		application.Shutdown();
		Console.WriteLine(
			"PASS: reusable WPF panel, shared LabStation style, LAN-only controls, shared controls, about windows, queue disconnect, plot and cursors");
		return 0;
	}

	private static void AssertApplicationIdentity(MainWindow window)
	{
		if(window.Title != "Siglent SDS1000CML Viewer v0.6.2")
		{
			throw new Exception("Unexpected main-window title: "+window.Title);
		}
		if(typeof(MainWindow).Assembly.GetName().Name !=
			"Siglent.SDS1000CML.Viewer")
		{
			throw new Exception(
				"Unexpected executable assembly name: "+
				typeof(MainWindow).Assembly.GetName().Name);
		}
		if(window.Icon is null)
		{
			throw new Exception("Main window does not use the supplied icon");
		}
	}

	private static void AssertReusablePanel(MainWindow window)
	{
		OscilloscopeView panel=Find<OscilloscopeView>(window);
		if(panel.Parent is not DockPanel)
		{
			throw new Exception(
				"The oscilloscope surface is not an embeddable panel in the standalone shell");
		}
		if(typeof(OscilloscopeView).BaseType != typeof(UserControl))
		{
			throw new Exception(
				"The oscilloscope surface is not a reusable WPF UserControl");
		}
	}

	private static void AssertConnectionControls(MainWindow window)
	{
		OscilloscopeView panel=Find<OscilloscopeView>(window);
		if(Descendants<ComboBox>(panel).Any())
		{
			throw new Exception(
				"A connection-method selector is still visible even though the oscilloscope supports only LAN");
		}
		TextBox address=Named<TextBox>(panel,"AddressTextBox");
		if(address.Text != "192.168.200.41")
		{
			throw new Exception("Unexpected default oscilloscope address");
		}
		Button connection=Named<Button>(panel,"ConnectionButton");
		if((string?)connection.Content != "Offline" || !connection.IsEnabled)
		{
			throw new Exception(
				"The offline connection control is missing or disabled");
		}
		if(Named<Button>(panel,"StartButton").IsEnabled)
		{
			throw new Exception("Start is enabled while offline");
		}
		if(Named<Button>(panel,"SaveButton").IsEnabled)
		{
			throw new Exception("CSV is enabled before a capture");
		}
	}

	private static void AssertConnectionAndCursorControlsShareOneRow(
		MainWindow window)
	{
		OscilloscopeView panel=Find<OscilloscopeView>(window);
		FrameworkElement[] controls=
		[
			Named<TextBox>(panel,"AddressTextBox"),
			Named<Button>(panel,"ConnectionButton"),
			Named<CheckBox>(panel,"Channel1CheckBox"),
			Named<CheckBox>(panel,"Channel2CheckBox"),
			Named<CheckBox>(panel,"LiveCheckBox"),
			Named<Button>(panel,"Cursor1Button"),
			Named<Button>(panel,"Cursor2Button"),
			Named<Button>(panel,"Cursor3Button"),
			Named<Button>(panel,"Cursor4Button")
		];
		double[] centers=controls
			.Select(control=>control.TranslatePoint(
				new Point(0,control.ActualHeight/2),
				panel).Y)
			.ToArray();
		if(centers.Max()-centers.Min()>2)
		{
			throw new Exception(
				"IP, connection, channel, preview and cursor controls are not in one row");
		}
	}

	private static void AssertCommonStyle(MainWindow window)
	{
		SolidColorBrush background=
			(SolidColorBrush)window.FindResource("KoradBackgroundBrush");
		SolidColorBrush input=
			(SolidColorBrush)window.FindResource("KoradInputBrush");
		if(background.Color != Color.FromRgb(104,104,104) ||
			input.Color != Color.FromRgb(133,133,133))
		{
			throw new Exception(
				"The shared Korad palette is not active in the oscilloscope");
		}
		OscilloscopeView panel=Find<OscilloscopeView>(window);
		Button connection=Named<Button>(panel,"ConnectionButton");
		if(connection.FontFamily.Source != "Consolas" ||
			connection.Background is not SolidColorBrush buttonBackground ||
			buttonBackground.Color != input.Color)
		{
			throw new Exception(
				"The oscilloscope controls do not use the Korad control style");
		}
	}

	private static void AssertSharedUiLibrary()
	{
		if(!typeof(MainWindow).Assembly.GetReferencedAssemblies()
			.Any(reference=>reference.Name == "LabStation.UI"))
		{
			throw new Exception(
				"The oscilloscope does not use the shared LabStation.UI module");
		}
	}

	private static void AssertSharedAboutMenuComponent(MainWindow window)
	{
		MenuItem about=Find<Menu>(window).Items.OfType<MenuItem>()
			.Single(item=>(string?)item.Header == "O aplikacji");
		if(about is not LabStation.UI.Controls.AboutMenuItem)
		{
			throw new Exception(
				"The main window does not use the reusable O aplikacji menu component");
		}
	}

	private static void AssertSharedControlDefaults()
	{
		LabStation.UI.Controls.NumericEditor editor=new();
		LabStation.UI.Controls.StatusLamps lamps=new()
		{
			IsOnline=true,
			IsOff=true,
			IsOn=true
		};
		StackPanel panel=new();
		panel.Children.Add(editor);
		panel.Children.Add(lamps);
		Window host=new()
		{
			Content=panel,
			ShowInTaskbar=false,
			WindowStyle=WindowStyle.None,
			Left=-10000,
			Top=-10000
		};
		host.Show();
		Pump();

		RepeatButton[] buttons=Descendants<RepeatButton>(editor).ToArray();
		if(buttons.Length != 2 ||
			(string?)buttons[0].Content != "▲" ||
			(string?)buttons[1].Content != "▼" ||
			buttons.Any(button=>button.Delay != 350 || button.Interval != 60))
		{
			throw new Exception(
				"The shared numeric editor no longer matches the accepted Korad control");
		}

		Color[] expected=
		[
			Color.FromRgb(255,255,0),
			Color.FromRgb(224,0,0),
			Color.FromRgb(0,255,0)
		];
		System.Windows.Shapes.Ellipse[] ellipses=
			Descendants<System.Windows.Shapes.Ellipse>(lamps).ToArray();
		if(ellipses.Length != 3 ||
			ellipses.Where((ellipse,index)=>
				ellipse.Width != 20 ||
				ellipse.Height != 20 ||
				(ellipse.Fill as SolidColorBrush)?.Color != expected[index])
			.Any())
		{
			throw new Exception(
				"The shared status lamps no longer match the accepted Korad control");
		}

		host.Close();
		Pump();
	}

	private static void AssertMenuPopupHasNoFrame(MainWindow window)
	{
		Menu menu=Find<Menu>(window);
		MenuItem about=menu.Items.OfType<MenuItem>()
			.Single(item=>(string?)item.Header == "O aplikacji");
		about.ApplyTemplate();
		about.IsSubmenuOpen=true;
		Pump();
		Popup popup=about.Template.FindName("PART_Popup",about) as Popup ??
			throw new Exception("The application menu has no popup template");
		Border frame=popup.Child as Border ??
			Descendants<Border>(popup.Child).FirstOrDefault() ??
			throw new Exception("The application menu popup has no chrome");
		if(frame.BorderThickness != new Thickness(0))
		{
			throw new Exception(
				"The expanded application menu still shows an outer frame");
		}
		about.IsSubmenuOpen=false;
		Pump();
	}

	private static void AssertAboutWindows(MainWindow window)
	{
		Menu menu=Find<Menu>(window);
		MenuItem about=menu.Items.OfType<MenuItem>()
			.SingleOrDefault(item=>(string?)item.Header == "O aplikacji")
			?? throw new Exception("Missing O aplikacji menu");
		string[] entries=about.Items.OfType<MenuItem>()
			.Select(item=>(string)item.Header)
			.ToArray();
		if(!entries.SequenceEqual(["Autor","Licencja"]))
		{
			throw new Exception("Unexpected O aplikacji menu entries");
		}

		AuthorWindow author=new(AppInformation.Presentation)
		{
			Owner=window,
			ShowInTaskbar=false,
			WindowStyle=WindowStyle.None,
			Left=-10000,
			Top=-10000
		};
		author.Show();
		Pump();
		string authorText=string.Join(
			"\n",
			Descendants<TextBlock>(author).Select(text=>text.Text));
		if(author.Title !=
			"Autor - Siglent SDS1000CML Viewer v0.6.2" ||
			!authorText.Contains("Mateusz Skipor",StringComparison.Ordinal) ||
			!authorText.Contains(
				"Inżynier technik elektroniki",
				StringComparison.Ordinal) ||
			!authorText.Contains(
				"mskiporsklep@op.pl",
				StringComparison.Ordinal))
		{
			throw new Exception("The Autor window does not match Korad");
		}
		author.Close();

		LicenseWindow license=new(AppInformation.Presentation)
		{
			Owner=window,
			ShowInTaskbar=false,
			WindowStyle=WindowStyle.None,
			Left=-10000,
			Top=-10000
		};
		license.Show();
		Pump();
		TextBox licenseText=Descendants<TextBox>(license).Single();
		if(license.Title !=
			"Licencja - Siglent SDS1000CML Viewer v0.6.2" ||
			!licenseText.IsReadOnly ||
			!licenseText.Text.Contains(
				"PolyForm Noncommercial License 1.0.0",
				StringComparison.Ordinal))
		{
			throw new Exception("The Licencja window does not match Korad");
		}
		license.Close();
	}

	private static void AssertPlotAndChannels(MainWindow window)
	{
		OscilloscopeView panel=Find<OscilloscopeView>(window);
		WavePlot plot=Find<WavePlot>(panel);
		int count=20000;
		double[] channel1=Enumerable.Range(0,count)
			.Select(index=>
				2*Math.Sin(index*2*Math.PI/5000))
			.ToArray();
		double[] channel2=Enumerable.Range(0,count)
			.Select(index=>
				Math.Sin(index*2*Math.PI/5000+0.6) > 0
					? 0.7
					: -0.7)
			.ToArray();
		plot.SetWaveforms(
		[
			new(1,channel1,1e-6,-0.01,DateTimeOffset.UnixEpoch),
			new(2,channel2,1e-6,-0.01,DateTimeOffset.UnixEpoch)
		]);
		(double fullMin,double fullMax)=plot.VisibleTimeRange;
		plot.ZoomAt(0.5,120);
		(double zoomMin,double zoomMax)=plot.VisibleTimeRange;
		if(zoomMax-zoomMin >= fullMax-fullMin)
		{
			throw new Exception(
				"Mouse-wheel zoom did not narrow the time axis");
		}
		plot.ClearCursors();
		plot.ActivateOrSelectCursor(0);
		plot.ActivateOrSelectCursor(1);
		plot.ActivateOrSelectCursor(2);
		if(!plot.ActiveCursorPairs().SequenceEqual([(1,2)]))
		{
			throw new Exception("Cursors 1,2,3 were not paired as 1-2");
		}
		plot.ClearCursors();
		plot.ActivateOrSelectCursor(1);
		plot.ActivateOrSelectCursor(2);
		if(!plot.ActiveCursorPairs().SequenceEqual([(2,3)]))
		{
			throw new Exception("Cursors 2,3 were not paired as 2-3");
		}
		plot.UnlockSelectedCursor(0.25);
		plot.MoveUnlockedCursor(0.75);
		plot.PlaceUnlockedCursor(0.60);
		if(plot.MovingCursor != -1 ||
			plot.CursorTime(2) is not double time ||
			time <= zoomMin ||
			time >= zoomMax)
		{
			throw new Exception(
				"Cursor unlock, follow and placement failed");
		}

		CheckBox channel1CheckBox=
			Named<CheckBox>(panel,"Channel1CheckBox");
		CheckBox channel2CheckBox=
			Named<CheckBox>(panel,"Channel2CheckBox");
		channel2CheckBox.IsChecked=false;
		Pump();
		if(!plot.VisibleWaveChannels.SequenceEqual([1]))
		{
			throw new Exception("Inactive CH2 remains visible in the plot");
		}
		channel1CheckBox.IsChecked=false;
		Pump();
		if(plot.HasWaveforms ||
			Descendants<Button>(panel)
				.Where(button=>
					((string?)button.Content)?.StartsWith(
						"Kursor ",
						StringComparison.Ordinal) == true)
				.Any(button=>button.IsEnabled))
		{
			throw new Exception(
				"Waveforms or cursors remain active with both channels disabled");
		}
		channel1CheckBox.IsChecked=true;
		channel2CheckBox.IsChecked=true;
		Pump();
	}

	private static void AssertOnlineDisconnectAvailableDuringPreview(
		MainWindow window)
	{
		OscilloscopeView panel=Find<OscilloscopeView>(window);
		FieldInfo scopeField=typeof(OscilloscopeView).GetField(
			"scope",
			BindingFlags.Instance|BindingFlags.NonPublic)
			?? throw new Exception("Missing scope session field");
		FieldInfo busyField=typeof(OscilloscopeView).GetField(
			"busy",
			BindingFlags.Instance|BindingFlags.NonPublic)
			?? throw new Exception("Missing preview busy field");
		MethodInfo updateEnabled=typeof(OscilloscopeView).GetMethod(
			"UpdateEnabled",
			BindingFlags.Instance|BindingFlags.NonPublic)
			?? throw new Exception("Missing control-state update method");
		NoOpTransport transport=new();
		ScopeClient client=new(transport);
		scopeField.SetValue(panel,client);
		busyField.SetValue(panel,true);
		updateEnabled.Invoke(panel,null);
		Button connection=Named<Button>(panel,"ConnectionButton");
		if((string?)connection.Content != "Online" ||
			!connection.IsEnabled)
		{
			throw new Exception(
				"Online button is disabled while a preview read is active");
		}
		MethodInfo connectOrDisconnect=
			typeof(OscilloscopeView).GetMethod(
				"ConnectOrDisconnect",
				BindingFlags.Instance|BindingFlags.NonPublic)
			?? throw new Exception("Missing connection lifecycle method");
		Task disconnect=(Task)(connectOrDisconnect.Invoke(panel,null)
			?? throw new Exception("Disconnect did not return a task"));
		Wait(disconnect,TimeSpan.FromSeconds(2));
		if(!disconnect.IsCompletedSuccessfully ||
			scopeField.GetValue(panel) is not null ||
			!transport.Disposed)
		{
			throw new Exception(
				"Offline request was not completed during a preview read: "+
				$"status={disconnect.Status}, "+
				$"scopeNull={scopeField.GetValue(panel) is null}, "+
				$"disposed={transport.Disposed}, "+
				$"error={disconnect.Exception}");
		}
		busyField.SetValue(panel,false);
		updateEnabled.Invoke(panel,null);
	}

	private static void AssertLayoutAndCapture(MainWindow window)
	{
		foreach((double width,double height,string name) in
			new (double Width,double Height,string Name)[]
		{
			(1016,780,"viewer-large.png"),
			(886,668,"viewer-minimum.png")
		})
		{
			window.Width=width;
			window.Height=height;
			window.UpdateLayout();
			Pump();
			OscilloscopeView panel=Find<OscilloscopeView>(window);
			foreach(Button button in
				Descendants<Button>(panel).Where(button=>button.IsVisible))
			{
				Point bottomRight=button.TranslatePoint(
					new Point(button.ActualWidth,button.ActualHeight),
					panel);
				if(bottomRight.X > panel.ActualWidth+0.5 ||
					bottomRight.Y > panel.ActualHeight+0.5)
				{
					throw new Exception(
						"Clipped button: "+button.Content);
				}
			}
			Directory.CreateDirectory("artifacts/qa");
			RenderTargetBitmap bitmap=new(
				(int)Math.Ceiling(window.ActualWidth),
				(int)Math.Ceiling(window.ActualHeight),
				96,
				96,
				PixelFormats.Pbgra32);
			bitmap.Render(window);
			PngBitmapEncoder encoder=new();
			encoder.Frames.Add(BitmapFrame.Create(bitmap));
			using FileStream stream=File.Create(
				Path.Combine("artifacts/qa",name));
			encoder.Save(stream);
		}
	}

	private static T Named<T>(FrameworkElement root,string name)
		where T : FrameworkElement
	{
		return root.FindName(name) as T ??
			throw new Exception("Missing control: "+name);
	}

	private static T Find<T>(DependencyObject root)
		where T : DependencyObject
	{
		return Descendants<T>(root).FirstOrDefault() ??
			throw new Exception("Missing visual element: "+typeof(T).Name);
	}

	private static IEnumerable<T> Descendants<T>(DependencyObject root)
		where T : DependencyObject
	{
		int count=VisualTreeHelper.GetChildrenCount(root);
		for(int index=0;index < count;index++)
		{
			DependencyObject child=VisualTreeHelper.GetChild(root,index);
			if(child is T match)
			{
				yield return match;
			}
			foreach(T descendant in Descendants<T>(child))
			{
				yield return descendant;
			}
		}
	}

	private static void Wait(Task task,TimeSpan timeout)
	{
		DateTime deadline=DateTime.UtcNow+timeout;
		while(!task.IsCompleted && DateTime.UtcNow < deadline)
		{
			Pump();
			Thread.Sleep(10);
		}
	}

	private static void Pump()
	{
		System.Windows.Threading.DispatcherFrame frame=new();
		Application.Current.Dispatcher.BeginInvoke(
			System.Windows.Threading.DispatcherPriority.ApplicationIdle,
			new Action(()=>frame.Continue=false));
		System.Windows.Threading.Dispatcher.PushFrame(frame);
	}
}

internal sealed class NoOpTransport : IInstrumentTransport
{
	public bool Disposed
	{
		get;private set;
	}

	public void Write(string command)
	{
		throw new InvalidOperationException("Unexpected write: "+command);
	}

	public byte[] Query(string command)
	{
		throw new InvalidOperationException("Unexpected query: "+command);
	}

	public void Dispose()
	{
		Disposed=true;
	}
}
