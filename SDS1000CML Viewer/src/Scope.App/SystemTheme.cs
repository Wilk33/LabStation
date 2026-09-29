using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Scope.App;

internal static class SystemTheme
{
	private const int UseImmersiveDarkMode=20;
	private const int UseImmersiveDarkModeBefore20H1=19;
	private const uint SwpNoSize=0x0001;
	private const uint SwpNoMove=0x0002;
	private const uint SwpNoZOrder=0x0004;
	private const uint SwpNoActivate=0x0010;
	private const uint SwpFrameChanged=0x0020;
	private const uint RdwInvalidate=0x0001;
	private const uint RdwUpdateNow=0x0100;
	private const uint RdwFrame=0x0400;
	private const string PersonalizeKey=@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
	private static readonly bool useDarkMode=ReadUseDarkMode();

	public static bool IsDark=>!SystemInformation.HighContrast && useDarkMode;

	public static void ApplyTo(Form form)
	{
		ArgumentNullException.ThrowIfNull(form);
		form.HandleCreated+=(_,_)=>ApplyWindowChrome(form);
		form.Shown+=(_,_)=>
		{
			ApplyWindowChrome(form);
			form.Invalidate(true);
			form.Update();
		};
		if(form.IsHandleCreated)
		{
			ApplyWindowChrome(form);
		}
	}

	public static void ApplyTo(MenuStrip menu)
	{
		ArgumentNullException.ThrowIfNull(menu);
		if(!IsDark || OperatingSystem.IsWindowsVersionAtLeast(10,0,22000))
		{
			menu.RenderMode=ToolStripRenderMode.System;
			return;
		}
		menu.BackColor=Color.FromArgb(35,35,35);
		menu.ForeColor=Color.WhiteSmoke;
		menu.Renderer=new DarkMenuRenderer();
		menu.HandleCreated+=(_,_)=>
		{
			menu.Invalidate(true);
			menu.Update();
		};
	}

	private static void ApplyWindowChrome(Form form)
	{
		if(!form.IsHandleCreated || !OperatingSystem.IsWindowsVersionAtLeast(10,0,17763))
		{
			return;
		}
		int enabled=IsDark ? 1 : 0;
		int result=DwmSetWindowAttribute(form.Handle,UseImmersiveDarkMode,ref enabled,sizeof(int));
		if(result < 0)
		{
			DwmSetWindowAttribute(form.Handle,UseImmersiveDarkModeBefore20H1,ref enabled,sizeof(int));
		}
		SetWindowPos(
			form.Handle,
			IntPtr.Zero,
			0,
			0,
			0,
			0,
			SwpNoSize|SwpNoMove|SwpNoZOrder|SwpNoActivate|SwpFrameChanged);
		RedrawWindow(form.Handle,IntPtr.Zero,IntPtr.Zero,RdwInvalidate|RdwUpdateNow|RdwFrame);
	}

	private static bool ReadUseDarkMode()
	{
		if(SystemInformation.HighContrast)
		{
			return false;
		}
		if(OperatingSystem.IsWindowsVersionAtLeast(10,0,22000) && Application.SystemColorMode == SystemColorMode.Dark)
		{
			return true;
		}
		try
		{
			using RegistryKey? key=Registry.CurrentUser.OpenSubKey(PersonalizeKey);
			return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
		}
		catch
		{
			return false;
		}
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(IntPtr windowHandle,int attribute,ref int attributeValue,int attributeSize);

	[DllImport("user32.dll",SetLastError=true)]
	[return:MarshalAs(UnmanagedType.Bool)]
	private static extern bool SetWindowPos(IntPtr windowHandle,IntPtr insertAfter,int x,int y,int width,int height,uint flags);

	[DllImport("user32.dll")]
	[return:MarshalAs(UnmanagedType.Bool)]
	private static extern bool RedrawWindow(IntPtr windowHandle,IntPtr updateRectangle,IntPtr updateRegion,uint flags);
}

internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
	public DarkMenuRenderer():base(new DarkMenuColors())
	{
	}

	protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
	{
		e.TextColor=e.Item.Enabled ? Color.WhiteSmoke : Color.Gray;
		base.OnRenderItemText(e);
	}
}

internal sealed class DarkMenuColors : ProfessionalColorTable
{
	private static readonly Color Background=Color.FromArgb(35,35,35);
	private static readonly Color Selection=Color.FromArgb(70,70,70);
	private static readonly Color Border=Color.FromArgb(95,95,95);
	public override Color ToolStripDropDownBackground=>Background;
	public override Color ImageMarginGradientBegin=>Background;
	public override Color ImageMarginGradientMiddle=>Background;
	public override Color ImageMarginGradientEnd=>Background;
	public override Color MenuBorder=>Border;
	public override Color MenuItemBorder=>Border;
	public override Color MenuItemSelected=>Selection;
	public override Color MenuItemSelectedGradientBegin=>Selection;
	public override Color MenuItemSelectedGradientEnd=>Selection;
	public override Color MenuItemPressedGradientBegin=>Background;
	public override Color MenuItemPressedGradientMiddle=>Background;
	public override Color MenuItemPressedGradientEnd=>Background;
	public override Color SeparatorDark=>Color.FromArgb(80,80,80);
	public override Color SeparatorLight=>Color.FromArgb(55,55,55);
}
