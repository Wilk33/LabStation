using System.Text;
using System.Text.Json;
using Scope.Core;

namespace Scope.App;

public sealed class MainForm : Form

{

	private readonly ComboBox mode=new()
	{
		DropDownStyle=ComboBoxStyle.DropDownList,
		Width=90
	};
	private readonly TextBox address=new()
	{
		Width=175,
		PlaceholderText="Adres IP oscyloskopu",
		Text="192.168.200.41"
	};

	private readonly Button connect=Button("Offline", 110);
	private readonly CheckBox ch1=new()
	{
		Text="CH1",
		Checked=true,
		AutoSize=true,
		ForeColor=Color.FromArgb(255, 220, 50),
		Padding=new(0, 6, 0, 0)
	};
	private readonly CheckBox ch2=new()
	{
		Text="CH2",
		Checked=true,
		AutoSize=true,
		ForeColor=Color.FromArgb(80, 225, 230),
		Padding=new(0, 6, 0, 0)
	};
	private readonly CheckBox live=new()
	{
		Text="Podgląd",
		Checked=true,
		AutoSize=true,
		Padding=new(8, 6, 0, 0)
	};
	private readonly Button start=Button("Start",90),stop=Button("Stop",90),auto=Button("Auto",90),capture=Button("Pobierz przebieg",190),save=Button("Zapisz CSV",150);
	private readonly Button[] cursorButtons=
	[
		Button("Kursor 1",92),
		Button("Kursor 2",92),
		Button("Kursor 3",92),
		Button("Kursor 4",92)
	];
	private readonly Label channel1Measurements=MeasurementLabel("CH1");
	private readonly Label channel2Measurements=MeasurementLabel("CH2");
	private readonly Label acquisition=new()
	{
		Text="Stan oscyloskopu: OFFLINE",
		Dock=DockStyle.Fill,
		AutoEllipsis=true,
		Font=new Font("Consolas", 11, FontStyle.Bold),
		ForeColor=Color.Silver,
		Padding=new(10, 5, 10, 0)
	};
	private readonly Label detail=new()
	{
		Text="CH1 + CH2 | CSV: czas [s], napięcie [V]",
		Dock=DockStyle.Fill,
		AutoEllipsis=true,
		Padding=new(10, 7, 10, 0)
	};
	private readonly WavePlot plot=new()
	{
		Dock=DockStyle.Fill,
		Margin=new(10)
	};
	private readonly System.Windows.Forms.Timer timer=new()
	{
		Interval=750
	};
	private readonly InstrumentOperationQueue operations=new();
	private ScopeClient? scope;
	private Waveform[]? captured;
	private ChannelMeasurements[] lastMeasurements=[];
	private bool busy, commandPending, closing, allowClose;
	private int[] Channels => new[]
{
ch1.Checked ? 1 : 0, ch2.Checked ? 2 : 0
}
.Where(x => x != 0).ToArray();
	private static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Siglent SDS1000CML Viewer", "settings.json");
	public MainForm()

	{

		SystemTheme.ApplyTo(this);
		Text="Siglent SDS1000CML Viewer v0.4.1";
		Font=new("Consolas", 10);
		BackColor=Color.FromArgb(97, 97, 97);
		ForeColor=Color.WhiteSmoke;
		ClientSize=new(1000,718);
		MinimumSize=new(870,606);
		StartPosition=FormStartPosition.CenterScreen;
		AutoScaleMode=AutoScaleMode.Dpi;
		Icon=AppAssets.CreateIcon();
		mode.Items.Add("LAN");
		mode.SelectedIndex=0;
		MenuStrip menu=CreateMenu();
		TableLayoutPanel layout=new()

		{

			Dock=DockStyle.Fill,
			ColumnCount=1,
			RowCount=7,
			BackColor=BackColor

		};
		layout.RowStyles.Add(new(SizeType.Absolute,50));
		layout.RowStyles.Add(new(SizeType.Absolute,42));
		layout.RowStyles.Add(new(SizeType.Absolute,52));
		layout.RowStyles.Add(new(SizeType.Percent,100));
		layout.RowStyles.Add(new(SizeType.Absolute,49));
		layout.RowStyles.Add(new(SizeType.Absolute,32));
		layout.RowStyles.Add(new(SizeType.Absolute,30));
		FlowLayoutPanel top=new()

		{

			Dock=DockStyle.Fill,
			Padding=new(10, 8, 8, 4),
			WrapContents=false

		};
		top.Controls.AddRange([mode, address, connect]);
		FlowLayoutPanel selection=new()

		{

			Dock=DockStyle.Fill,
			Padding=new(14, 0, 8, 0),
			WrapContents=false

		};
		selection.Controls.AddRange([ch1,ch2,live,..cursorButtons]);
		TableLayoutPanel readings=new()
		{
			Dock=DockStyle.Fill,
			ColumnCount=1,
			RowCount=2,
			Padding=new(10,0,8,0),
			BackColor=BackColor
		};
		readings.RowStyles.Add(new(SizeType.Percent,50));
		readings.RowStyles.Add(new(SizeType.Percent,50));
		readings.Controls.Add(channel1Measurements,0,0);
		readings.Controls.Add(channel2Measurements,0,1);
		FlowLayoutPanel actions=new()

		{

			Dock=DockStyle.Fill,
			Padding=new(10, 8, 8, 4),
			WrapContents=false

		};
		actions.Controls.AddRange([start, stop, auto, capture, save]);
		layout.Controls.Add(top,0,0);
		layout.Controls.Add(selection,0,1);
		layout.Controls.Add(readings,0,2);
		layout.Controls.Add(plot,0,3);
		layout.Controls.Add(actions,0,4);
		layout.Controls.Add(detail,0,5);
		layout.Controls.Add(acquisition,0,6);
		Controls.Add(layout);
		Controls.Add(menu);
		MainMenuStrip=menu;
		foreach (Control c in new Control[]
{
mode, address
})

		{

			c.BackColor=Color.FromArgb(145, 145, 145);
			c.ForeColor=Color.White;
			c.Font=Font;

		}

		for(int index=0;index < cursorButtons.Length;index++)
		{
			int cursorIndex=index;
			cursorButtons[index].Click+=(_,_)=>plot.ActivateOrSelectCursor(cursorIndex);
		}
		plot.CursorStateChanged+=(_,_)=>UpdateCursorButtons();
		ToolTip tips=new();
		tips.SetToolTip(connect, "Kliknij, aby połączyć lub rozłączyć oscyloskop przez LAN.");
		tips.SetToolTip(start, "Wznawia poprzedni tryb wyzwalania. Gdy nieznany, uruchamia TRMD AUTO.");
		tips.SetToolTip(auto, "Auto Setup oscyloskopu. Zmienia ustawienia pomiaru.");
		tips.SetToolTip(live, "Cykliczne odczyty. Odznaczenie zatrzymuje tylko odświeżanie aplikacji.");
		tips.SetToolTip(capture, "Pobiera pełny dostępny blok próbek. Dla wspólnej akwizycji CH1/CH2 najpierw wybierz Stop.");
		tips.SetToolTip(save, "Zapisuje ostatni ręcznie pobrany przebieg, niezależnie od kolejnych odświeżeń podglądu.");
		for(int index=0;index < cursorButtons.Length;index++)
		{
			tips.SetToolTip(cursorButtons[index],$"Kursor {index+1}: kliknij, aby włączyć lub wybrać. Ponowne kliknięcie wybranego kursora wyłącza go.");
		}
		tips.SetToolTip(plot,"Rolka: przybliżenie osi czasu. PPM: odblokuj wybrany kursor. LPM: ustaw kursor.");
		connect.Click+=async (_, _) => await ConnectOrDisconnect();
		start.Click+=async (_,_)=>await Command(scope=>scope.Start());
		stop.Click+=async (_,_)=>await Command(scope=>scope.Stop());
		auto.Click+=async (_,_)=>await Command(scope=>scope.Auto());
		capture.Click+=async (_, _) => await CaptureWaveform(true);
		save.Click+=async (_, _) => await SaveCsv();
		timer.Tick+=async (_, _) =>

{

	if (!busy && !commandPending && scope != null && live.Checked && Channels.Length > 0)
		await CaptureWaveform(false);

};
		ch1.CheckedChanged+=(_,_)=>ChannelSelectionChanged();
		ch2.CheckedChanged+=(_,_)=>ChannelSelectionChanged();
		LoadSettings();
		ChannelSelectionChanged();
		timer.Start();

	}

	private static Label MeasurementLabel(string channel)=>new()
	{
		Text=channel+": Vpp -- | Vrms -- | Hz -- | Vmin -- | Vmax -- | Duty --",
		Dock=DockStyle.Fill,
		AutoEllipsis=true,
		Padding=new(4,3,4,0),
		Font=new Font("Consolas",10,FontStyle.Bold),
		ForeColor=channel == "CH1" ? Color.FromArgb(255,220,50) : Color.FromArgb(80,225,230)
	};
	private static Button Button(string text, int width) => new()

	{

		Text=text,
		Width=width,
		Height=32,
		FlatStyle=FlatStyle.Standard,
		BackColor=Color.FromArgb(192, 192, 192),
		ForeColor=Color.Black,
		UseVisualStyleBackColor=false

	};
	private void UpdateEnabled()

	{

		bool connected=scope != null;
		connect.Enabled=connected ? !commandPending && !closing : !busy && !commandPending && !closing;
		connect.Text=connected ? "Online" : "Offline";
		mode.Enabled=address.Enabled=!busy && !connected;
		start.Enabled=stop.Enabled=auto.Enabled=connected && !commandPending && !closing;
		capture.Enabled=connected && !commandPending && Channels.Length > 0;
		save.Enabled=captured != null && !busy && !commandPending;
		foreach(Button cursorButton in cursorButtons)
		{
			cursorButton.Enabled=plot.HasWaveforms;
		}

	}

	private void ChannelSelectionChanged()
	{
		int[] channels=Channels;
		plot.SetVisibleChannels(channels);
		channel1Measurements.Visible=ch1.Checked;
		channel2Measurements.Visible=ch2.Checked;
		detail.Visible=channels.Length > 0;
		UpdateCursorButtons();
		UpdateEnabled();
	}

	private MenuStrip CreateMenu()

	{

		MenuStrip menu=new()

		{

			RenderMode=ToolStripRenderMode.System,
			Font=SystemFonts.MenuFont

		};
		SystemTheme.ApplyTo(menu);

		ToolStripMenuItem about=new("O Aplikacji");
		ToolStripMenuItem author=new("Autor");
		ToolStripMenuItem license=new("Licencja");

		author.Click+=(_, _) => new InfoForm("Autor", "Mateusz Skipor\r\nInżynier Technik Elektroniki\r\nmskiporsklep@op.pl").Show(this);
		license.Click+=(_, _) => new InfoForm("Licencja", AppAssets.LicenseText, true).Show(this);
		about.DropDownItems.AddRange([author, license]);
		menu.Items.Add(about);
		return menu;


	}

	private async Task ConnectOrDisconnect()

	{

		if(scope != null)
		{
			if(commandPending || closing)
			{
				return;
			}
			commandPending=true;
			UpdateEnabled();
			try
			{
				await Disconnect();
			}
			catch(Exception ex)
			{
				ShowError(ex,true);
			}
			finally
			{
				commandPending=false;
				UpdateEnabled();
			}
			return;
		}
		if(busy || commandPending || closing)
			return;
		busy=true;
		UpdateEnabled();
		try

		{

			string host=address.Text.Trim();
			if (host.Length == 0)
				throw new InvalidOperationException("Wpisz adres IP oscyloskopu.");
			scope=await Task.Run(() =>

{

	IInstrumentTransport transport=new Vxi11Transport(host);
	ScopeClient client=new(transport);
	try

	{

		client.Initialize();
		return client;

	}

	catch
	{
		client.Dispose();
		throw;
	}


});
			AcquisitionState state=await Task.Run(scope.AcquisitionStatus);
			UpdateAcquisition(state);
			SaveSettings();

		}

		catch (Exception ex)
		{
			ShowError(ex, true);
		}

		finally
		{
			busy=false;
			UpdateEnabled();
		}


	}

	private async Task Disconnect()

	{

		ScopeClient? old=scope;
		scope=null;
		plot.Stale=true;
		plot.Invalidate();
		UpdateMeasurementRows(lastMeasurements,true);
		if (old != null)
		{
			await operations.RunCommandAsync(()=>Task.Run(old.Dispose));
		}
		UpdateAcquisition(AcquisitionState.Unknown);

	}

	private async Task Command(Action<ScopeClient> action)

	{

		if (commandPending || scope == null)
			return;
		commandPending=true;
		UpdateEnabled();
		ScopeClient client=scope;
		try

		{
			AcquisitionState state=AcquisitionState.Unknown;
			await operations.RunCommandAsync(async () =>
			{
				await Task.Run(() =>
				{
					action(client);
					state=client.AcquisitionStatus();
				});
			});
			if (ReferenceEquals(scope, client))
			{
				UpdateAcquisition(state);
			}

		}
		catch (Exception ex)
		{
			if (ReferenceEquals(scope, client))
				await Disconnect();
			ShowError(ex, true);
		}
		finally
		{
			commandPending=false;
			UpdateEnabled();
		}


	}
	private sealed record ScopeSnapshot(Waveform[] Waves,ChannelMeasurements[] Measurements,AcquisitionState State);

	private async Task CaptureWaveform(bool manual)
	{
		if(manual)
		{
			await CaptureManualWaveform();
		}
		else
		{
			await CapturePreview();
		}
	}

	private async Task CaptureManualWaveform()
	{
		if(commandPending || scope == null)
		{
			return;
		}
		int[] channels=Channels;
		if(channels.Length == 0)
		{
			return;
		}
		commandPending=true;
		UpdateEnabled();
		ScopeClient client=scope;
		try
		{
			ScopeSnapshot? snapshot=null;
			await operations.RunCommandAsync(async()=>
			{
				snapshot=await Task.Run(()=>ReadSnapshot(client,channels));
			});
			if(snapshot != null && ReferenceEquals(scope,client))
			{
				ApplySnapshot(snapshot,true);
			}
		}
		catch(InvalidOperationException ex)
		{
			ShowError(ex,true);
		}
		catch(Exception ex)
		{
			if(ReferenceEquals(scope,client))
			{
				await Disconnect();
			}
			ShowError(ex,true);
		}
		finally
		{
			commandPending=false;
			UpdateEnabled();
		}
	}

	private async Task CapturePreview()
	{
		if(busy || commandPending || scope == null)
		{
			return;
		}
		int[] channels=Channels;
		if(channels.Length == 0)
		{
			return;
		}
		busy=true;
		UpdateEnabled();
		ScopeClient client=scope;
		try
		{
			ScopeSnapshot? snapshot=null;
			bool completed=await operations.TryRunPreviewAsync(async()=>
			{
				snapshot=await Task.Run(()=>ReadSnapshot(client,channels));
			});
			if(completed && snapshot != null && ReferenceEquals(scope,client))
			{
				ApplySnapshot(snapshot,false);
			}
		}
		catch(InvalidOperationException ex)
		{
			live.Checked=false;
			ShowError(ex,false);
		}
		catch(Exception ex)
		{
			if(ReferenceEquals(scope,client))
			{
				await Disconnect();
			}
			ShowError(ex,false);
		}
		finally
		{
			busy=false;
			UpdateEnabled();
		}
	}

	private static ScopeSnapshot ReadSnapshot(ScopeClient client,int[] channels)
	{
		Waveform[] waves=client.Capture(channels);
		int[] capturedChannels=waves.Select(wave=>wave.Channel).ToArray();
		ChannelMeasurements[] measurements=client.Measurements(capturedChannels);
		AcquisitionState state=client.AcquisitionStatus();
		return new(waves,measurements,state);
	}

	private void ApplySnapshot(ScopeSnapshot snapshot,bool manual)
	{
		plot.SetWaveforms(snapshot.Waves);
		lastMeasurements=snapshot.Measurements;
		UpdateMeasurementRows(lastMeasurements,false);
		UpdateAcquisition(snapshot.State);
		if(manual)
		{
			captured=snapshot.Waves;
		}
		detail.Text=string.Join(" | ",snapshot.Waves.Select(wave=>$"CH{wave.Channel}: {wave.Volts.Length:N0} pkt"))+" | "+DateTime.Now.ToString("HH:mm:ss");
		UpdateCursorButtons();
	}

	private void UpdateCursorButtons()
	{
		for(int index=0;index < cursorButtons.Length;index++)
		{
			Button button=cursorButtons[index];
			bool active=plot.IsCursorActive(index);
			bool selected=plot.SelectedCursor == index;
			bool moving=plot.MovingCursor == index;
			button.Text=$"Kursor {index+1}"+(moving ? " RUCH" : "");
			button.FlatStyle=FlatStyle.Flat;
			button.FlatAppearance.BorderSize=selected ? 3 : 1;
			button.FlatAppearance.BorderColor=selected ? Color.White : Color.FromArgb(80,80,80);
			button.BackColor=active ? WavePlot.CursorColor(index) : Color.FromArgb(192,192,192);
			button.ForeColor=active ? Color.Black : Color.Black;
		}
	}

	private void UpdateMeasurementRows(ChannelMeasurements[] measurements,bool stale)
	{
		channel1Measurements.Text=MeasurementText("CH1",measurements.SingleOrDefault(value=>value.Channel == 1),stale);
		channel2Measurements.Text=MeasurementText("CH2",measurements.SingleOrDefault(value=>value.Channel == 2),stale);
	}

	private static string MeasurementText(string channel,ChannelMeasurements? value,bool stale)
	{
		string prefix=channel+(stale && value != null ? " [nieaktualne]" : "");
		if(value == null)
		{
			return prefix+": Vpp -- | Vrms -- | Hz -- | Vmin -- | Vmax -- | Duty --";
		}
		return prefix+": Vpp "+FormatMeasurement(value.Vpp,"V")+
			" | Vrms "+FormatMeasurement(value.Vrms,"V")+
			" | Hz "+FormatMeasurement(value.Frequency,"Hz")+
			" | Vmin "+FormatMeasurement(value.Minimum,"V")+
			" | Vmax "+FormatMeasurement(value.Maximum,"V")+
			" | Duty "+(value.DutyPercent.HasValue
				? value.DutyPercent.Value.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+" %"
				: "--");
	}

	private static string FormatMeasurement(double? value,string unit)
	{
		return value.HasValue ? FormatEngineering(value.Value,unit) : "--";
	}
	private static string FormatEngineering(double value,string unit)
	{
		double absolute=Math.Abs(value);
		(double factor,string prefix)=absolute switch
		{
			>=1e6=>(1e6,"M"),
			>=1e3=>(1e3,"k"),
			>=1=>(1,""),
			>=1e-3=>(1e-3,"m"),
			>=1e-6=>(1e-6,"µ"),
			>0=>(1e-9,"n"),
			_=>(1,"")
		};
		return (value/factor).ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" "+prefix+unit;
	}
	private void UpdateAcquisition(AcquisitionState state)

	{

		acquisition.Text="Stan oscyloskopu: "+state switch
		{
			AcquisitionState.Start => "START",
			AcquisitionState.Stop => "STOP",
			_ => scope == null ? "OFFLINE" : "NIEZNANY"
		};
		acquisition.ForeColor=state switch
		{
			AcquisitionState.Start => Color.LimeGreen,
			AcquisitionState.Stop => Color.Gold,
			_ => Color.Silver
		};

	}
	private async Task SaveCsv()

	{

		if (busy || commandPending || captured == null)
			return;
		using SaveFileDialog dialog=new()

		{

			Filter="Przebieg CSV (*.csv)|*.csv",
			FileName="SDS1102CML_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".csv"

		};
		busy=true;
		UpdateEnabled();
		Waveform[] data=captured;
		try

		{

			if (dialog.ShowDialog(this) != DialogResult.OK)
				return;
			await Task.Run(() =>

{

	string temp=dialog.FileName+"."+Guid.NewGuid().ToString("N")+".tmp";
	try

	{

		using (StreamWriter writer=new(temp, false, new UTF8Encoding(true)))
			Csv.Write(writer, data);
		File.Move(temp, dialog.FileName, true);

	}

	finally
	{
		if (File.Exists(temp))
			File.Delete(temp);
	}


});

		}

		catch (Exception ex)
		{
			ShowError(ex, true);
		}

		finally
		{
			busy=false;
			UpdateEnabled();
		}


	}

	private void ShowError(Exception ex, bool dialog)

	{
		if (dialog)
			MessageBox.Show(this, ex.Message, "Siglent SDS1000CML Viewer", MessageBoxButtons.OK, MessageBoxIcon.Warning);

	}

	private void LoadSettings()

	{

		try

		{

			if (File.Exists(SettingsPath))

			{

				var settings=JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(SettingsPath));
				address.Text=settings?.GetValueOrDefault("address") ?? "";

			}


		}

		catch (Exception)
		{

		}


	}

	private void SaveSettings()

	{

		try

		{

			Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
			File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new Dictionary<string, string>
{

{
"address", address.Text.Trim()
}

}));

		}

		catch (Exception)
		{

		}


	}

	protected override async void OnFormClosing(FormClosingEventArgs e)

	{

		if (!allowClose)

		{

			e.Cancel=true;
			if (closing)
				return;
			closing=true;
			timer.Stop();
			UpdateEnabled();
			while (busy || commandPending)
				await Task.Delay(100);
			await Disconnect();
			allowClose=true;
			Close();
			return;

		}

		operations.Dispose();
		timer.Dispose();
		base.OnFormClosing(e);

	}


}
