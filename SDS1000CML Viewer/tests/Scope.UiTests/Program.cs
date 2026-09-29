using Scope.App;
using Scope.Core;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Reflection;
using Microsoft.Win32;

internal static class Program

{

	[STAThread]
	private static int Main(string[] args)

	{

		if(OperatingSystem.IsWindowsVersionAtLeast(10,0,22000))
		{
			Application.SetColorMode(SystemColorMode.System);
		}
		ApplicationConfiguration.Initialize();
		Application.SetDefaultFont(new Font("Consolas", 10));
		using MainForm form=new();
		form.ShowInTaskbar=false;
		form.Opacity=0;
		form.Show();
		Application.DoEvents();
		Control[] controls=Walk(form).ToArray();
		AssertApplicationIdentity(form);
		AssertDarkTitleBar(form);
		AssertConnectionControls(controls);
		AssertOnlineDisconnectAvailableDuringPreview(form,controls);
		if(OperatingSystem.IsWindowsVersionAtLeast(10,0,22000) && Application.ColorMode != SystemColorMode.System)
		{
			throw new Exception("Application does not inherit the Windows color mode");
		}
		AssertAboutMenu(form);
		if (controls.OfType<Button>().First(b => b.Text == "Zapisz CSV").Enabled)
			throw new Exception("CSV enabled before capture");
		if (controls.OfType<Button>().First(b => b.Text == "Start").Enabled)
			throw new Exception("Start enabled offline");
		WavePlot plot=controls.OfType<WavePlot>().Single();
		for(int j=0;j < 2;j++)

		{

			form.ClientSize=j == 0 ? new(1000,718) : new(870,606);
			Application.DoEvents();
			int n=20000;
			double[] a=Enumerable.Range(0, n).Select(i => 2*Math.Sin(i*2*Math.PI/5000)).ToArray();
			double[] b=Enumerable.Range(0, n).Select(i => Math.Sin(i*2*Math.PI/5000+0.6) > 0 ? 0.7 : -0.7).ToArray();
			plot.SetWaveforms([new(1, a, 1e-6, -0.01, DateTimeOffset.UnixEpoch), new(2, b, 1e-6, -0.01, DateTimeOffset.UnixEpoch)]);
			if(j == 0)
			{
				AssertPlotInteraction(plot);
				AssertChannelVisibility(plot,controls);
			}
			foreach (Label label in controls.OfType<Label>())
				if (label.Text.StartsWith("CH1 +"))
					label.Text="PRZYKŁAD WYGLĄDU - dane syntetyczne, bez połączenia z oscyloskopem";
			Application.DoEvents();
			foreach (Button button in controls.OfType<Button>().Where(c => c.Visible))

			{

				if (button.Right > button.Parent!.ClientSize.Width || button.Bottom > button.Parent.ClientSize.Height)
					throw new Exception($"Clipped button: {button.Text}; bounds={button.Bounds}; parent={button.Parent!.ClientRectangle}");

			}

			Directory.CreateDirectory("artifacts/qa");
			using Bitmap bitmap=new(form.Width, form.Height);
			form.DrawToBitmap(bitmap, new(0, 0, form.Width, form.Height));
			bitmap.Save($"artifacts/qa/viewer-{j}.png", ImageFormat.Png);

		}

		form.Close();
		Application.DoEvents();
		Console.WriteLine("PASS: application identity, offline controls, about windows, two window sizes, render with full sample buffers");
		return 0;

	}

	private static void AssertChannelVisibility(WavePlot plot,Control[] controls)
	{
		CheckBox ch1=controls.OfType<CheckBox>().Single(control=>control.Text == "CH1");
		CheckBox ch2=controls.OfType<CheckBox>().Single(control=>control.Text == "CH2");
		Label ch1Measurements=controls.OfType<Label>().Single(label=>label.Text.StartsWith("CH1: Vpp",StringComparison.Ordinal));
		Label ch2Measurements=controls.OfType<Label>().Single(label=>label.Text.StartsWith("CH2: Vpp",StringComparison.Ordinal));
		Button[] cursorButtons=controls.OfType<Button>().Where(button=>button.Text.StartsWith("Kursor ",StringComparison.Ordinal)).ToArray();

		ch2.Checked=false;
		Application.DoEvents();
		if(!plot.VisibleWaveChannels.SequenceEqual([1]) || !ch1Measurements.Visible || ch2Measurements.Visible)
		{
			throw new Exception("An inactive CH2 remains visible in the plot or measurement rows");
		}

		ch1.Checked=false;
		Application.DoEvents();
		if(plot.HasWaveforms || plot.VisibleWaveChannels.Length != 0 || ch1Measurements.Visible || ch2Measurements.Visible || cursorButtons.Any(button=>button.Enabled))
		{
			throw new Exception("The waveform, measurements or cursor buttons remain active with both channels disabled");
		}

		ch1.Checked=true;
		ch2.Checked=true;
		Application.DoEvents();
		if(!plot.VisibleWaveChannels.SequenceEqual(new[] { 1,2 }))
		{
			throw new Exception("Enabled channels were not restored");
		}
	}
	private static void AssertPlotInteraction(WavePlot plot)

	{

		(double fullMin, double fullMax)=plot.VisibleTimeRange;
		plot.ZoomAt(0.5, 120);
		(double zoomMin, double zoomMax)=plot.VisibleTimeRange;
		if (zoomMax-zoomMin >= fullMax-fullMin)
			throw new Exception("Mouse-wheel zoom did not narrow the time axis");
		plot.ClearCursors();
		plot.ActivateOrSelectCursor(0);
		plot.ActivateOrSelectCursor(1);
		plot.ActivateOrSelectCursor(2);
		if (!plot.ActiveCursorPairs().SequenceEqual(new[] { (1, 2) }))
			throw new Exception("Cursors 1,2,3 were not paired as 1-2");
		plot.ClearCursors();
		plot.ActivateOrSelectCursor(1);
		plot.ActivateOrSelectCursor(2);
		if (!plot.ActiveCursorPairs().SequenceEqual(new[] { (2, 3) }))
			throw new Exception("Cursors 2,3 were not paired as 2-3");
		plot.UnlockSelectedCursor(0.25);
		plot.MoveUnlockedCursor(0.75);
		plot.PlaceUnlockedCursor(0.60);
		if (plot.MovingCursor != -1 || plot.CursorTime(2) is not double time || time <= zoomMin || time >= zoomMax)
			throw new Exception("Cursor unlock, follow and placement failed");

	}
	private static void AssertApplicationIdentity(MainForm form)
	{
		if(form.Text != "Siglent SDS1000CML Viewer v0.4.1")
		{
			throw new Exception("Unexpected main-window title: "+form.Text);
		}
		if(typeof(MainForm).Assembly.GetName().Name != "Siglent.SDS1000CML.Viewer")
		{
			throw new Exception("Unexpected executable assembly name: "+typeof(MainForm).Assembly.GetName().Name);
		}
		using Icon expected=new("siglent_sds1102cml+.ico");
		using Bitmap expectedBitmap=expected.ToBitmap();
		using Bitmap actualBitmap=form.Icon!.ToBitmap();
		if (!SHA256.HashData(BitmapBytes(expectedBitmap)).SequenceEqual(SHA256.HashData(BitmapBytes(actualBitmap))))
			throw new Exception("Main window does not use the supplied ICO icon");
	}

	private static void AssertOnlineDisconnectAvailableDuringPreview(MainForm form,Control[] controls)
	{
		FieldInfo scopeField=typeof(MainForm).GetField("scope",BindingFlags.Instance|BindingFlags.NonPublic)
			?? throw new Exception("Missing scope session field");
		FieldInfo busyField=typeof(MainForm).GetField("busy",BindingFlags.Instance|BindingFlags.NonPublic)
			?? throw new Exception("Missing preview busy field");
		MethodInfo updateEnabled=typeof(MainForm).GetMethod("UpdateEnabled",BindingFlags.Instance|BindingFlags.NonPublic)
			?? throw new Exception("Missing control-state update method");
		NoOpTransport transport=new();
		using ScopeClient client=new(transport);
		scopeField.SetValue(form,client);
		busyField.SetValue(form,true);
		updateEnabled.Invoke(form,null);
		Button connection=controls.OfType<Button>().Single(button=>button.Text == "Online");
		if(!connection.Enabled)
		{
			throw new Exception("Online button is disabled while a preview read is active");
		}
		MethodInfo connectOrDisconnect=typeof(MainForm).GetMethod("ConnectOrDisconnect",BindingFlags.Instance|BindingFlags.NonPublic)
			?? throw new Exception("Missing connection lifecycle method");
		Task disconnect=(Task)(connectOrDisconnect.Invoke(form,null)
			?? throw new Exception("Disconnect did not return a task"));
		DateTime deadline=DateTime.UtcNow.AddSeconds(2);
		while(!disconnect.IsCompleted && DateTime.UtcNow < deadline)
		{
			Application.DoEvents();
			Thread.Sleep(10);
		}
		if(!disconnect.IsCompletedSuccessfully || scopeField.GetValue(form) != null || !transport.Disposed)
		{
			throw new Exception("Offline request was not completed while a preview read was active");
		}
		scopeField.SetValue(form,null);
		busyField.SetValue(form,false);
		updateEnabled.Invoke(form,null);
	}

	private static void AssertDarkTitleBar(Form form)

	{

		if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763) || OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
			return;
		using RegistryKey? key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
		if (key?.GetValue("AppsUseLightTheme") is not int light || light != 0)
			return;
		int dark=0;
		int result=DwmGetWindowAttribute(form.Handle, 20, out dark, sizeof(int));
		if (result != 0)
			result=DwmGetWindowAttribute(form.Handle, 19, out dark, sizeof(int));
		if (result != 0 || dark != 1)
			throw new Exception($"The Windows 10 title bar did not receive dark mode: result={result}, value={dark}");

	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
	private static void AssertConnectionControls(Control[] controls)
	{
		Button connection=controls.OfType<Button>().SingleOrDefault(button => button.Text == "Offline")
			?? throw new Exception("The connection button does not show only Offline");
		if (controls.OfType<Label>().Any(label => label.Text == "Offline"))
			throw new Exception("Offline is still displayed as a separate label");
		if (controls.OfType<Label>().Any(label => label.Text == "Podgląd nie zmienia ustawień oscyloskopu"))
			throw new Exception("The removed preview note is still visible");
		if (controls.OfType<Label>().Any(label => label.Text.Contains("PPM odblokuj", StringComparison.Ordinal)))
			throw new Exception("The unrequested visible cursor instruction is still present");
		if (controls.OfType<TableLayoutPanel>().Any(panel => panel.RowCount == 8))
			throw new Exception("The bottom message row is still present");
		if (!controls.OfType<Label>().Any(label => label.Text == "Stan oscyloskopu: OFFLINE"))
			throw new Exception("Missing acquisition status at the bottom of the main window");
		if (!controls.OfType<Label>().Any(label => label.Text.StartsWith("CH1: Vpp")) || !controls.OfType<Label>().Any(label => label.Text.StartsWith("CH2: Vpp")))
			throw new Exception("Missing CH1/CH2 measurement rows");
		if (controls.OfType<Button>().Count(button => button.Text.StartsWith("Kursor ")) != 4)
			throw new Exception("Missing four cursor buttons");
		if (controls.OfType<Label>().Any(label => label.Text.Contains("USB", StringComparison.OrdinalIgnoreCase)))
			throw new Exception("USB implementation status is still visible in the main window");
		ComboBox mode=controls.OfType<ComboBox>().Single(combo => combo.Items.Cast<object>().Any(item => item?.ToString() == "LAN"));
		if (mode.Items.Count != 1 || mode.Items[0]?.ToString() != "LAN")
			throw new Exception("USB can still be selected as a connection method");
	}

	private static void AssertAboutMenu(MainForm form)
	{
		MenuStrip menu=form.MainMenuStrip ?? throw new Exception("Missing application toolbar");
		if (menu.RenderMode != ToolStripRenderMode.System && menu.Renderer.GetType().Name != "DarkMenuRenderer")
			throw new Exception("The application toolbar does not use the Windows renderer or its Windows 10 dark fallback");
		ToolStripMenuItem about=menu.Items.OfType<ToolStripMenuItem>().SingleOrDefault(item => item.Text == "O Aplikacji")
			?? throw new Exception("Missing O Aplikacji menu");
		ToolStripMenuItem author=about.DropDownItems.OfType<ToolStripMenuItem>().SingleOrDefault(item => item.Text == "Autor")
			?? throw new Exception("Missing Autor menu item");
		ToolStripMenuItem license=about.DropDownItems.OfType<ToolStripMenuItem>().SingleOrDefault(item => item.Text == "Licencja")
			?? throw new Exception("Missing Licencja menu item");

		author.PerformClick();
		Application.DoEvents();
		Form authorWindow=Application.OpenForms.Cast<Form>().Single(window => window.Text == "Autor");
		AssertDarkTitleBar(authorWindow);
		AssertInformationWindow(authorWindow, form, "Mateusz Skipor", "Inżynier Technik Elektroniki", "mskiporsklep@op.pl");
		CaptureWindow(authorWindow, "artifacts/qa/author.png");
		authorWindow.Close();

		license.PerformClick();
		Application.DoEvents();
		Form licenseWindow=Application.OpenForms.Cast<Form>().Single(window => window.Text == "Licencja");
		AssertDarkTitleBar(licenseWindow);
		AssertInformationWindow(licenseWindow, form, "PolyForm Noncommercial License 1.0.0");
		CaptureWindow(licenseWindow, "artifacts/qa/license.png");
		licenseWindow.Close();
	}

	private static void AssertInformationWindow(Form window, MainForm main, params string[] expectedText)
	{
		Control[] controls=Walk(window).ToArray();
		string text=string.Join("\n", controls.Select(control => control.Text));
		if (window.BackColor != main.BackColor || window.Font.Name != "Consolas")
			throw new Exception(window.Text+" window does not match the application style");
		if (window.ShowIcon)
			throw new Exception(window.Text+" window still shows a title-bar graphic");
		if (controls.OfType<PictureBox>().Any())
			throw new Exception(window.Text+" window still contains artwork");
		if (controls.OfType<Button>().Any())
			throw new Exception(window.Text+" window still contains a close button");
		foreach (string expected in expectedText)
			if (!text.Contains(expected, StringComparison.Ordinal))
				throw new Exception(window.Text+" window is missing: "+expected);
	}

	private static void CaptureWindow(Form window, string path)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		using Bitmap bitmap=new(window.Width, window.Height);
		window.DrawToBitmap(bitmap, new(0, 0, window.Width, window.Height));
		bitmap.Save(path, ImageFormat.Png);
	}

	private static byte[] BitmapBytes(Bitmap bitmap)
	{
		using MemoryStream stream=new();
		bitmap.Save(stream, ImageFormat.Png);
		return stream.ToArray();
	}

	private static IEnumerable<Control> Walk(Control root)

	{

		foreach (Control control in root.Controls)

		{

			yield return control;
			foreach (Control child in Walk(control))
				yield return child;

		}


	}


}

internal sealed class NoOpTransport : IInstrumentTransport
{
	public bool Disposed
	{
		get; private set;
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
