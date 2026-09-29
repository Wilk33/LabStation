using System.Drawing.Drawing2D;
using Scope.Core;

namespace Scope.App;

public sealed class WavePlot : Control
{
	private static readonly Color[] CursorColors=
	[
		Color.OrangeRed,
		Color.LimeGreen,
		Color.DeepSkyBlue,
		Color.Magenta
	];
	private readonly bool[] cursorActive=new bool[4];
	private readonly double?[] cursorTimes=new double?[4];
	private HashSet<int> visibleChannels=[1,2];
	private Waveform[] waves=[];
	private Point? hover;
	private double fullTimeMin;
	private double fullTimeMax=1;
	private double viewTimeMin;
	private double viewTimeMax=1;
	private int selectedCursor=-1;
	private int movingCursor=-1;

	public event EventHandler? CursorStateChanged;

	[System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
	public bool Stale
	{
		get;set;
	}

	public bool HasWaveforms=>waves.Any(wave=>visibleChannels.Contains(wave.Channel));
	public int[] VisibleWaveChannels=>waves
		.Where(wave=>visibleChannels.Contains(wave.Channel))
		.Select(wave=>wave.Channel)
		.Distinct()
		.OrderBy(channel=>channel)
		.ToArray();
	public int SelectedCursor=>selectedCursor;
	public int MovingCursor=>movingCursor;
	public (double Min,double Max) VisibleTimeRange=>(viewTimeMin,viewTimeMax);

	public WavePlot()
	{
		DoubleBuffered=true;
		BackColor=Color.FromArgb(43,43,43);
		ForeColor=Color.WhiteSmoke;
		TabStop=true;
		SetStyle(ControlStyles.ResizeRedraw,true);
	}

	public void SetWaveforms(Waveform[] value)
	{
		bool wasFull=!HasWaveforms || Nearly(viewTimeMin,fullTimeMin) && Nearly(viewTimeMax,fullTimeMax);
		waves=value;
		Stale=false;
		UpdateTimeRange(wasFull);
		Invalidate();
	}

	public void SetVisibleChannels(int[] channels)
	{
		if(channels.Any(channel=>channel != 1 && channel != 2))
		{
			throw new ArgumentException("Wybierz CH1 lub CH2.",nameof(channels));
		}
		visibleChannels=channels.Distinct().ToHashSet();
		UpdateTimeRange(true);
		if(visibleChannels.Count == 0)
		{
			ClearCursors();
			return;
		}
		Invalidate();
	}

	public void ZoomAt(double fraction,int wheelDelta)
	{
		if(!HasWaveforms || wheelDelta == 0)
		{
			return;
		}
		fraction=Math.Clamp(fraction,0,1);
		double fullSpan=fullTimeMax-fullTimeMin;
		double span=viewTimeMax-viewTimeMin;
		double factor=wheelDelta > 0 ? 0.8 : 1.25;
		double newSpan=Math.Clamp(span*factor,fullSpan/10000,fullSpan);
		double anchor=viewTimeMin+span*fraction;
		viewTimeMin=anchor-newSpan*fraction;
		viewTimeMax=viewTimeMin+newSpan;
		ClampView();
		Invalidate();
	}

	public void ActivateOrSelectCursor(int index)
	{
		ValidateCursor(index);
		if(!HasWaveforms)
		{
			return;
		}
		if(cursorActive[index] && selectedCursor == index && movingCursor != index)
		{
			cursorActive[index]=false;
			cursorTimes[index]=null;
			selectedCursor=Enumerable.Range(0,4).FirstOrDefault(i=>cursorActive[i],-1);
			if(movingCursor == index)
			{
				movingCursor=-1;
				Capture=false;
			}
		}
		else
		{
			cursorActive[index]=true;
			selectedCursor=index;
			cursorTimes[index]??=viewTimeMin+(viewTimeMax-viewTimeMin)*(index+1)/5;
		}
		NotifyCursorStateChanged();
	}

	public void ClearCursors()
	{
		Array.Fill(cursorActive,false);
		Array.Fill(cursorTimes,null);
		selectedCursor=-1;
		movingCursor=-1;
		Capture=false;
		NotifyCursorStateChanged();
	}

	public static Color CursorColor(int index)
	{
		ValidateCursor(index);
		return CursorColors[index];
	}
	public bool IsCursorActive(int index)
	{
		ValidateCursor(index);
		return cursorActive[index];
	}

	public double? CursorTime(int index)
	{
		ValidateCursor(index);
		return cursorTimes[index];
	}

	public (int First,int Second)[] ActiveCursorPairs()
	{
		int[] active=Enumerable.Range(0,4).Where(index=>cursorActive[index]).ToArray();
		return Enumerable.Range(0,active.Length/2)
			.Select(pair=>(active[pair*2]+1,active[pair*2+1]+1))
			.ToArray();
	}

	public void UnlockSelectedCursor(double fraction)
	{
		if(selectedCursor < 0 || !cursorActive[selectedCursor] || !HasWaveforms)
		{
			return;
		}
		movingCursor=selectedCursor;
		MoveUnlockedCursor(fraction);
		NotifyCursorStateChanged();
	}

	public void MoveUnlockedCursor(double fraction)
	{
		if(movingCursor < 0 || !HasWaveforms)
		{
			return;
		}
		fraction=Math.Clamp(fraction,0,1);
		cursorTimes[movingCursor]=viewTimeMin+(viewTimeMax-viewTimeMin)*fraction;
		NotifyCursorStateChanged();
	}

	public void PlaceUnlockedCursor(double fraction)
	{
		if(movingCursor < 0)
		{
			return;
		}
		MoveUnlockedCursor(fraction);
		movingCursor=-1;
		Capture=false;
		NotifyCursorStateChanged();
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		base.OnMouseEnter(e);
		Focus();
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		hover=e.Location;
		if(movingCursor >= 0)
		{
			MoveUnlockedCursor(FractionAt(e.X));
		}
		else
		{
			Invalidate();
		}
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		base.OnMouseLeave(e);
		if(movingCursor < 0)
		{
			hover=null;
			Invalidate();
		}
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		base.OnMouseDown(e);
		if(!PlotArea.Contains(e.Location))
		{
			return;
		}
		if(e.Button == MouseButtons.Right)
		{
			UnlockSelectedCursor(FractionAt(e.X));
			Capture=movingCursor >= 0;
		}
		else if(e.Button == MouseButtons.Left && movingCursor >= 0)
		{
			PlaceUnlockedCursor(FractionAt(e.X));
		}
	}

	protected override void OnMouseWheel(MouseEventArgs e)
	{
		base.OnMouseWheel(e);
		if(PlotArea.Contains(e.Location))
		{
			ZoomAt(FractionAt(e.X),e.Delta);
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		Graphics graphics=e.Graphics;
		Rectangle area=PlotArea;
		using Pen grid=new(Color.FromArgb(75,75,75));
		using Brush text=new SolidBrush(ForeColor);
		if(area.Width < 20 || area.Height < 20)
		{
			return;
		}
		for(int index=0;index <= 10;index++)
		{
			float x=area.Left+area.Width*index/10f;
			graphics.DrawLine(grid,x,area.Top,x,area.Bottom);
		}
		for(int index=0;index <= 8;index++)
		{
			float y=area.Top+area.Height*index/8f;
			graphics.DrawLine(grid,area.Left,y,area.Right,y);
		}
		if(!HasWaveforms)
		{
			if(visibleChannels.Count > 0)
			{
				string label="Połącz oscyloskop, aby wyświetlić przebieg";
				SizeF size=graphics.MeasureString(label,Font);
				graphics.DrawString(label,Font,text,area.Left+(area.Width-size.Width)/2,area.Top+area.Height/2);
			}
			return;
		}
		Waveform[] visibleWaves=CurrentWaves();
		double low=visibleWaves.Min(wave=>wave.Volts.Min());
		double high=visibleWaves.Max(wave=>wave.Volts.Max());
		double margin=Math.Max((high-low)*0.12,0.01);
		low-=margin;
		high+=margin;
		for(int index=0;index <= 8;index++)
		{
			double voltage=high-(high-low)*index/8;
			graphics.DrawString(Engineering(voltage,"V"),Font,text,2,area.Top+area.Height*index/8f-8);
		}
		for(int index=0;index <= 4;index++)
		{
			string label=Engineering(viewTimeMin+(viewTimeMax-viewTimeMin)*index/4,"s");
			SizeF size=graphics.MeasureString(label,Font);
			float labelX=Math.Clamp(area.Left+area.Width*index/4f-size.Width/2,0,Math.Max(0,Width-size.Width-4));
			graphics.DrawString(label,Font,text,labelX,area.Bottom+10);
		}
		graphics.SetClip(area);
		foreach(Waveform wave in visibleWaves)
		{
			DrawWaveform(graphics,area,wave,low,high);
		}
		DrawCursors(graphics,area);
		if(hover is Point point && area.Contains(point))
		{
			using Pen cross=new(Color.FromArgb(160,160,160))
			{
				DashStyle=DashStyle.Dot
			};
			graphics.DrawLine(cross,point.X,area.Top,point.X,area.Bottom);
		}
		graphics.ResetClip();
		graphics.DrawString("Napięcie",Font,text,area.Left,5);
		graphics.DrawString("Czas",Font,text,area.Right-40,area.Bottom+34);
		if(hover is Point hoverPoint && area.Contains(hoverPoint))
		{
			double time=TimeAt(FractionAt(hoverPoint.X));
			graphics.DrawString(CursorValues("t",time),Font,text,area.Left,area.Bottom+34);
		}
		DrawCursorInformation(graphics,area);
		if(Stale)
		{
			using Brush badge=new SolidBrush(Color.DarkRed);
			graphics.FillRectangle(badge,area.Left+10,area.Top+10,260,25);
			graphics.DrawString("DANE NIEAKTUALNE / OFFLINE",Font,text,area.Left+15,area.Top+14);
		}
	}

	private Rectangle PlotArea=>new(78,28,Math.Max(1,Width-102),Math.Max(1,Height-86));

	private void DrawWaveform(Graphics graphics,Rectangle area,Waveform wave,double low,double high)
	{
		using Pen line=new(wave.Channel == 1 ? Color.FromArgb(255,220,50) : Color.FromArgb(80,225,230),1.25f);
		float X(int index)=>(float)(area.Left+(wave.Start+index*wave.Interval-viewTimeMin)/(viewTimeMax-viewTimeMin)*area.Width);
		float Y(double voltage)=>(float)(area.Bottom-(voltage-low)/(high-low)*area.Height);
		int first=Math.Max(0,(int)Math.Floor((viewTimeMin-wave.Start)/wave.Interval));
		int last=Math.Min(wave.Volts.Length-1,(int)Math.Ceiling((viewTimeMax-wave.Start)/wave.Interval));
		if(last < first)
		{
			return;
		}
		int count=last-first+1;
		if(count <= area.Width*2)
		{
			PointF[] points=Enumerable.Range(first,count)
				.Select(index=>new PointF(X(index),Y(wave.Volts[index])))
				.ToArray();
			if(points.Length > 1)
			{
				graphics.DrawLines(line,points);
			}
			else
			{
				graphics.DrawEllipse(line,points[0].X-1,points[0].Y-1,2,2);
			}
			return;
		}
		for(int x=0;x < area.Width;x++)
		{
			double begin=viewTimeMin+(viewTimeMax-viewTimeMin)*x/area.Width;
			double end=viewTimeMin+(viewTimeMax-viewTimeMin)*(x+1)/area.Width;
			int columnFirst=Math.Max(first,(int)Math.Ceiling((begin-wave.Start)/wave.Interval));
			int columnLast=Math.Min(last+1,(int)Math.Ceiling((end-wave.Start)/wave.Interval));
			if(columnLast <= columnFirst)
			{
				continue;
			}
			double minimum=wave.Volts[columnFirst];
			double maximum=minimum;
			for(int index=columnFirst+1;index < columnLast;index++)
			{
				minimum=Math.Min(minimum,wave.Volts[index]);
				maximum=Math.Max(maximum,wave.Volts[index]);
			}
			graphics.DrawLine(line,area.Left+x,Y(minimum),area.Left+x,Y(maximum)+0.5f);
		}
	}

	private void DrawCursors(Graphics graphics,Rectangle area)
	{
		for(int index=0;index < cursorActive.Length;index++)
		{
			if(!cursorActive[index] || cursorTimes[index] is not double time || time < viewTimeMin || time > viewTimeMax)
			{
				continue;
			}
			float x=(float)(area.Left+(time-viewTimeMin)/(viewTimeMax-viewTimeMin)*area.Width);
			using Pen pen=new(CursorColors[index],selectedCursor == index ? 2.5f : 1.5f)
			{
				DashStyle=movingCursor == index ? DashStyle.Dash : DashStyle.Solid
			};
			graphics.DrawLine(pen,x,area.Top,x,area.Bottom);
			using Brush brush=new SolidBrush(CursorColors[index]);
			graphics.DrawString("K"+(index+1),Font,brush,x+3,area.Top+3);
		}
	}

	private void DrawCursorInformation(Graphics graphics,Rectangle area)
	{
		List<(string Text,Color Color)> lines=[];
		for(int index=0;index < cursorActive.Length;index++)
		{
			if(cursorActive[index] && cursorTimes[index] is double time)
			{
				string marker=movingCursor == index ? " [RUCH]" : selectedCursor == index ? " [WYBRANY]" : "";
				lines.Add((CursorValues("K"+(index+1),time)+marker,CursorColors[index]));
			}
		}
		foreach((int first,int second) in ActiveCursorPairs())
		{
			double firstTime=cursorTimes[first-1]!.Value;
			double secondTime=cursorTimes[second-1]!.Value;
			string line=$"ΔK{first}-K{second}: Δt="+Engineering(secondTime-firstTime,"s");
			foreach(Waveform wave in CurrentWaves().OrderBy(item=>item.Channel))
			{
				double? firstVoltage=VoltageAt(wave,firstTime);
				double? secondVoltage=VoltageAt(wave,secondTime);
				if(firstVoltage.HasValue && secondVoltage.HasValue)
				{
					line+=$"  ΔCH{wave.Channel}="+Engineering(secondVoltage.Value-firstVoltage.Value,"V");
				}
			}
			lines.Add((line,Color.WhiteSmoke));
		}
		if(lines.Count == 0)
		{
			return;
		}
		float width=lines.Max(line=>graphics.MeasureString(line.Text,Font).Width)+12;
		float height=lines.Count*(Font.Height+2)+8;
		float left=Math.Max(area.Left+5,area.Right-width-5);
		using Brush background=new SolidBrush(Color.FromArgb(215,25,25,25));
		graphics.FillRectangle(background,left,area.Top+5,width,height);
		for(int index=0;index < lines.Count;index++)
		{
			using Brush brush=new SolidBrush(lines[index].Color);
			graphics.DrawString(lines[index].Text,Font,brush,left+6,area.Top+9+index*(Font.Height+2));
		}
	}

	private string CursorValues(string name,double time)
	{
		string result=name+"="+Engineering(time,"s");
		foreach(Waveform wave in CurrentWaves().OrderBy(item=>item.Channel))
		{
			double? voltage=VoltageAt(wave,time);
			if(voltage.HasValue)
			{
				result+=$"  CH{wave.Channel}="+Engineering(voltage.Value,"V");
			}
		}
		return result;
	}

	private static double? VoltageAt(Waveform wave,double time)
	{
		int index=(int)Math.Round((time-wave.Start)/wave.Interval);
		return index >= 0 && index < wave.Volts.Length ? wave.Volts[index] : null;
	}

	private Waveform[] CurrentWaves()=>waves.Where(wave=>visibleChannels.Contains(wave.Channel)).ToArray();

	private void UpdateTimeRange(bool resetView)
	{
		Waveform[] current=CurrentWaves();
		if(current.Length == 0)
		{
			fullTimeMin=0;
			fullTimeMax=1;
			viewTimeMin=0;
			viewTimeMax=1;
			return;
		}
		fullTimeMin=current.Min(wave=>wave.Start);
		fullTimeMax=current.Max(wave=>wave.Start+(wave.Volts.Length-1)*wave.Interval);
		if(fullTimeMax <= fullTimeMin)
		{
			fullTimeMax=fullTimeMin+current[0].Interval;
		}
		if(resetView)
		{
			viewTimeMin=fullTimeMin;
			viewTimeMax=fullTimeMax;
		}
		else
		{
			ClampView();
		}
		for(int index=0;index < cursorTimes.Length;index++)
		{
			if(cursorTimes[index] is double time)
			{
				cursorTimes[index]=Math.Clamp(time,fullTimeMin,fullTimeMax);
			}
		}
	}

	private double FractionAt(int x)
	{
		Rectangle area=PlotArea;
		return Math.Clamp((double)(x-area.Left)/area.Width,0,1);
	}

	private double TimeAt(double fraction)=>viewTimeMin+(viewTimeMax-viewTimeMin)*fraction;

	private void ClampView()
	{
		double fullSpan=fullTimeMax-fullTimeMin;
		double span=Math.Min(viewTimeMax-viewTimeMin,fullSpan);
		if(viewTimeMin < fullTimeMin)
		{
			viewTimeMin=fullTimeMin;
			viewTimeMax=viewTimeMin+span;
		}
		if(viewTimeMax > fullTimeMax)
		{
			viewTimeMax=fullTimeMax;
			viewTimeMin=viewTimeMax-span;
		}
	}

	private void NotifyCursorStateChanged()
	{
		Invalidate();
		CursorStateChanged?.Invoke(this,EventArgs.Empty);
	}

	private static void ValidateCursor(int index)
	{
		if(index < 0 || index >= 4)
		{
			throw new ArgumentOutOfRangeException(nameof(index));
		}
	}

	private static bool Nearly(double first,double second)
	{
		double scale=Math.Max(1,Math.Max(Math.Abs(first),Math.Abs(second)));
		return Math.Abs(first-second) <= scale*1e-12;
	}

	private static string Engineering(double value,string unit)
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
}
