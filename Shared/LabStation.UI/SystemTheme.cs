using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace LabStation.UI;

public static class SystemTheme
{
	private const int UseImmersiveDarkMode=20;
	private const int UseImmersiveDarkModeBefore20H1=19;
	private const string PersonalizeKey=
		@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
	private static bool useDarkMode;

	public static void Initialize(ResourceDictionary resources)
	{
		ArgumentNullException.ThrowIfNull(resources);
		useDarkMode=ReadUseDarkMode();
		resources["SystemChromeBackgroundBrush"]=new SolidColorBrush(
			useDarkMode
				? Color.FromRgb(32,32,32)
				: Color.FromRgb(240,240,240));
		resources["SystemChromeForegroundBrush"]=new SolidColorBrush(
			useDarkMode
				? Colors.White
				: Colors.Black);
		resources["SystemChromeHoverBrush"]=new SolidColorBrush(
			useDarkMode
				? Color.FromRgb(62,62,62)
				: Color.FromRgb(218,218,218));
		resources["SystemChromeDisabledBrush"]=new SolidColorBrush(
			useDarkMode
				? Color.FromRgb(122,122,122)
				: Color.FromRgb(112,112,112));
		resources["SystemChromeBorderBrush"]=new SolidColorBrush(
			useDarkMode
				? Color.FromRgb(112,112,112)
				: Color.FromRgb(128,128,128));
		resources["SystemChromeSeparatorBrush"]=new SolidColorBrush(
			useDarkMode
				? Color.FromRgb(89,89,89)
				: Color.FromRgb(190,190,190));
		resources[SystemColors.MenuBrushKey]=
			resources["SystemChromeBackgroundBrush"];
		resources[SystemColors.MenuTextBrushKey]=
			resources["SystemChromeForegroundBrush"];
	}

	public static void ApplyTo(Window window)
	{
		ArgumentNullException.ThrowIfNull(window);
		if(new WindowInteropHelper(window).Handle != nint.Zero)
		{
			ApplyWindowChrome(window);
			return;
		}
		window.SourceInitialized+=OnSourceInitialized;
	}

	private static void OnSourceInitialized(object? sender,EventArgs eventArgs)
	{
		if(sender is not Window window)
		{
			return;
		}
		window.SourceInitialized-=OnSourceInitialized;
		ApplyWindowChrome(window);
	}

	private static void ApplyWindowChrome(Window window)
	{
		if(!OperatingSystem.IsWindows())
		{
			return;
		}
		nint handle=new WindowInteropHelper(window).Handle;
		int enabled=useDarkMode ? 1 : 0;
		int result=DwmSetWindowAttribute(
			handle,
			UseImmersiveDarkMode,
			ref enabled,
			sizeof(int));
		if(result < 0)
		{
			DwmSetWindowAttribute(
				handle,
				UseImmersiveDarkModeBefore20H1,
				ref enabled,
				sizeof(int));
		}
	}

	private static bool ReadUseDarkMode()
	{
		try
		{
			object? value=Registry.GetValue(
				PersonalizeKey,
				"AppsUseLightTheme",
				1);
			return value is int intValue && intValue == 0;
		}
		catch
		{
			return false;
		}
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(
		nint windowHandle,
		int attribute,
		ref int attributeValue,
		int attributeSize);
}
