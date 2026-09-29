namespace Scope.App;

internal sealed class InfoForm : Form

{

	public InfoForm(string title, string content, bool large=false)

	{

		SystemTheme.ApplyTo(this);
		Text=title;
		Font=new("Consolas", 10);
		BackColor=Color.FromArgb(97, 97, 97);
		ForeColor=Color.WhiteSmoke;
		ShowIcon=false;
		ClientSize=large ? new(760, 560) : new(440, 160);
		MinimumSize=large ? new(620, 440) : Size;
		StartPosition=FormStartPosition.CenterParent;
		FormBorderStyle=FormBorderStyle.FixedDialog;
		MaximizeBox=false;
		MinimizeBox=false;
		ShowInTaskbar=false;
		Padding=new(16);

		TextBox information=new()

		{

			Text=content,
			ReadOnly=true,
			Multiline=true,
			ScrollBars=large ? ScrollBars.Vertical : ScrollBars.None,
			WordWrap=true,
			Dock=DockStyle.Fill,
			BackColor=Color.FromArgb(82, 82, 82),
			ForeColor=Color.WhiteSmoke,
			BorderStyle=BorderStyle.FixedSingle,
			Font=Font,
			Margin=new(0),
			TabStop=false

		};
		Controls.Add(information);

	}



}
