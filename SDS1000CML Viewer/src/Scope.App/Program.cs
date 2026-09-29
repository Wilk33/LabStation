namespace Scope.App;

internal static class Program

{

	[STAThread]
	private static void Main(string[] args)

	{

		if(OperatingSystem.IsWindowsVersionAtLeast(10,0,22000))
		{
			Application.SetColorMode(SystemColorMode.System);
		}
		ApplicationConfiguration.Initialize();
		Application.SetDefaultFont(new Font("Consolas", 10));
		Application.ThreadException+=(sender, e) => MessageBox.Show(e.Exception.Message, "Błąd aplikacji", MessageBoxButtons.OK, MessageBoxIcon.Error);
		Application.Run(new MainForm());

	}


}
