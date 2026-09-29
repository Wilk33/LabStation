using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Scope.Core;

namespace Scope.App;

public sealed class WavePlot : FrameworkElement
{
	private static readonly Color[] CursorColors=
	[
		Colors.OrangeRed,
		Colors.LimeGreen,
		Colors.DeepSkyBlue,
		Colors.Magenta
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

	public bool Stale
	{
		get;set;
	}

	public bool HasWaveforms=>
		waves.Any(wave=>visibleChannels.Contains(wave.Channel));
	public int[] VisibleWaveChannels=>waves
		.Where(wave=>visibleChannels.Contains(wave.Channel))
		.Select(wave=>wave.Channel)
		.Distinct()
		.OrderBy(channel=>channel)
		.ToArray();
	public int SelectedCursor=>selectedCursor;
	public int MovingCursor=>movingCursor;
	public (double Min,double Max) VisibleTimeRange=>
		(viewTimeMin,viewTimeMax);

	public WavePlot()
	{
		Focusable=true;
		ClipToBounds=true;
	}

	public void SetWaveforms(Waveform[] value)
	{
		bool wasFull=
			!HasWaveforms ||
			Nearly(viewTimeMin,fullTimeMin) &&
			Nearly(viewTimeMax,fullTimeMax);
		waves=value;
		Stale=false;
		UpdateTimeRange(wasFull);
		InvalidateVisual();
	}

	public void SetVisibleChannels(int[] channels)
	{
		if(channels.Any(channel=>channel != 1 && channel != 2))
		{
			throw new ArgumentException(
				"Wybierz CH1 lub CH2.",
				nameof(channels));
		}
		visibleChannels=channels.Distinct().ToHashSet();
		UpdateTimeRange(true);
		if(visibleChannels.Count == 0)
		{
			ClearCursors();
			return;
		}
		InvalidateVisual();
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
		double newSpan=Math.Clamp(
			span*factor,
			fullSpan/10000,
			fullSpan);
		double anchor=viewTimeMin+span*fraction;
		viewTimeMin=anchor-newSpan*fraction;
		viewTimeMax=viewTimeMin+newSpan;
		ClampView();
		InvalidateVisual();
	}

	public void ActivateOrSelectCursor(int index)
	{
		ValidateCursor(index);
		if(!HasWaveforms)
		{
			return;
		}
		if(cursorActive[index] &&
			selectedCursor == index &&
			movingCursor != index)
		{
			cursorActive[index]=false;
			cursorTimes[index]=null;
			selectedCursor=Enumerable.Range(0,4)
				.FirstOrDefault(cursor=>cursorActive[cursor],-1);
		}
		else
		{
			cursorActive[index]=true;
			selectedCursor=index;
			cursorTimes[index]??=
				viewTimeMin+(viewTimeMax-viewTimeMin)*(index+1)/5;
		}
		NotifyCursorStateChanged();
	}

	public void ClearCursors()
	{
		Array.Fill(cursorActive,false);
		Array.Fill(cursorTimes,null);
		selectedCursor=-1;
		movingCursor=-1;
		ReleaseMouseCapture();
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
		int[] active=Enumerable.Range(0,4)
			.Where(index=>cursorActive[index])
			.ToArray();
		return Enumerable.Range(0,active.Length/2)
			.Select(pair=>(active[pair*2]+1,active[pair*2+1]+1))
			.ToArray();
	}

	public void UnlockSelectedCursor(double fraction)
	{
		if(selectedCursor < 0 ||
			!cursorActive[selectedCursor] ||
			!HasWaveforms)
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
		cursorTimes[movingCursor]=
			viewTimeMin+(viewTimeMax-viewTimeMin)*fraction;
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
		ReleaseMouseCapture();
		NotifyCursorStateChanged();
	}

	protected override void OnMouseEnter(MouseEventArgs eventArgs)
	{
		base.OnMouseEnter(eventArgs);
		Focus();
	}

	protected override void OnMouseMove(MouseEventArgs eventArgs)
	{
		base.OnMouseMove(eventArgs);
		hover=eventArgs.GetPosition(this);
		if(movingCursor >= 0)
		{
			MoveUnlockedCursor(FractionAt(hover.Value.X));
		}
		else
		{
			InvalidateVisual();
		}
	}

	protected override void OnMouseLeave(MouseEventArgs eventArgs)
	{
		base.OnMouseLeave(eventArgs);
		if(movingCursor < 0)
		{
			hover=null;
			InvalidateVisual();
		}
	}

	protected override void OnMouseDown(MouseButtonEventArgs eventArgs)
	{
		base.OnMouseDown(eventArgs);
		Point position=eventArgs.GetPosition(this);
		if(!PlotArea.Contains(position))
		{
			return;
		}
		if(eventArgs.ChangedButton == MouseButton.Right)
		{
			UnlockSelectedCursor(FractionAt(position.X));
			if(movingCursor >= 0)
			{
				CaptureMouse();
			}
		}
		else if(
			eventArgs.ChangedButton == MouseButton.Left &&
			movingCursor >= 0)
		{
			PlaceUnlockedCursor(FractionAt(position.X));
		}
	}

	protected override void OnMouseWheel(MouseWheelEventArgs eventArgs)
	{
		base.OnMouseWheel(eventArgs);
		Point position=eventArgs.GetPosition(this);
		if(PlotArea.Contains(position))
		{
			ZoomAt(FractionAt(position.X),eventArgs.Delta);
			eventArgs.Handled=true;
		}
	}

	protected override void OnRender(DrawingContext drawingContext)
	{
		base.OnRender(drawingContext);
		drawingContext.DrawRectangle(
			new SolidColorBrush(Color.FromRgb(43,43,43)),
			null,
			new Rect(0,0,ActualWidth,ActualHeight));
		Rect area=PlotArea;
		if(area.Width < 20 || area.Height < 20)
		{
			return;
		}
		Pen grid=new(
			new SolidColorBrush(Color.FromRgb(75,75,75)),
			1);
		for(int index=0;index <= 10;index++)
		{
			double x=area.Left+area.Width*index/10;
			drawingContext.DrawLine(
				grid,
				new Point(x,area.Top),
				new Point(x,area.Bottom));
		}
		for(int index=0;index <= 8;index++)
		{
			double y=area.Top+area.Height*index/8;
			drawingContext.DrawLine(
				grid,
				new Point(area.Left,y),
				new Point(area.Right,y));
		}
		if(!HasWaveforms)
		{
			if(visibleChannels.Count > 0)
			{
				FormattedText text=Text(
					"Połącz oscyloskop, aby wyświetlić przebieg",
					Brushes.WhiteSmoke);
				drawingContext.DrawText(
					text,
					new Point(
						area.Left+(area.Width-text.Width)/2,
						area.Top+(area.Height-text.Height)/2));
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
			drawingContext.DrawText(
				Text(Engineering(voltage,"V"),Brushes.WhiteSmoke),
				new Point(2,area.Top+area.Height*index/8-8));
		}
		for(int index=0;index <= 4;index++)
		{
			FormattedText label=Text(
				Engineering(
					viewTimeMin+(viewTimeMax-viewTimeMin)*index/4,
					"s"),
				Brushes.WhiteSmoke);
			double labelX=Math.Clamp(
				area.Left+area.Width*index/4-label.Width/2,
				0,
				Math.Max(0,ActualWidth-label.Width-4));
			drawingContext.DrawText(
				label,
				new Point(labelX,area.Bottom+8));
		}
		drawingContext.PushClip(new RectangleGeometry(area));
		foreach(Waveform wave in visibleWaves)
		{
			DrawWaveform(drawingContext,area,wave,low,high);
		}
		DrawCursors(drawingContext,area);
		if(hover is Point hoverPoint && area.Contains(hoverPoint))
		{
			Pen cross=new(
				new SolidColorBrush(Color.FromRgb(160,160,160)),
				1)
			{
				DashStyle=DashStyles.Dot
			};
			drawingContext.DrawLine(
				cross,
				new Point(hoverPoint.X,area.Top),
				new Point(hoverPoint.X,area.Bottom));
		}
		drawingContext.Pop();
		drawingContext.DrawText(
			Text("Napięcie",Brushes.WhiteSmoke),
			new Point(area.Left,5));
		drawingContext.DrawText(
			Text("Czas",Brushes.WhiteSmoke),
			new Point(area.Right-40,area.Bottom+32));
		if(hover is Point point && area.Contains(point))
		{
			double time=TimeAt(FractionAt(point.X));
			drawingContext.DrawText(
				Text(CursorValues("t",time),Brushes.WhiteSmoke),
				new Point(area.Left,area.Bottom+32));
		}
		DrawCursorInformation(drawingContext,area);
		if(Stale)
		{
			drawingContext.DrawRectangle(
				Brushes.DarkRed,
				null,
				new Rect(area.Left+10,area.Top+10,260,25));
			drawingContext.DrawText(
				Text(
					"DANE NIEAKTUALNE / OFFLINE",
					Brushes.WhiteSmoke),
				new Point(area.Left+15,area.Top+14));
		}
	}

	private Rect PlotArea=>new(
		78,
		28,
		Math.Max(1,ActualWidth-102),
		Math.Max(1,ActualHeight-86));

	private void DrawWaveform(
		DrawingContext drawingContext,
		Rect area,
		Waveform wave,
		double low,
		double high)
	{
		Pen line=new(
			wave.Channel == 1
				? new SolidColorBrush(Color.FromRgb(255,220,50))
				: new SolidColorBrush(Color.FromRgb(80,225,230)),
			1.25);
		double X(int index)=>
			area.Left+
			(wave.Start+index*wave.Interval-viewTimeMin)/
			(viewTimeMax-viewTimeMin)*area.Width;
		double Y(double voltage)=>
			area.Bottom-(voltage-low)/(high-low)*area.Height;
		int first=Math.Max(
			0,
			(int)Math.Floor((viewTimeMin-wave.Start)/wave.Interval));
		int last=Math.Min(
			wave.Volts.Length-1,
			(int)Math.Ceiling((viewTimeMax-wave.Start)/wave.Interval));
		if(last < first)
		{
			return;
		}
		int count=last-first+1;
		if(count <= area.Width*2)
		{
			StreamGeometry geometry=new();
			using(StreamGeometryContext context=geometry.Open())
			{
				context.BeginFigure(
					new Point(X(first),Y(wave.Volts[first])),
					false,
					false);
				if(count > 1)
				{
					context.PolyLineTo(
						Enumerable.Range(first+1,count-1)
							.Select(index=>new Point(
								X(index),
								Y(wave.Volts[index])))
							.ToArray(),
						true,
						false);
				}
			}
			drawingContext.DrawGeometry(null,line,geometry);
			return;
		}
		for(int x=0;x < area.Width;x++)
		{
			double begin=
				viewTimeMin+(viewTimeMax-viewTimeMin)*x/area.Width;
			double end=
				viewTimeMin+(viewTimeMax-viewTimeMin)*(x+1)/area.Width;
			int columnFirst=Math.Max(
				first,
				(int)Math.Ceiling((begin-wave.Start)/wave.Interval));
			int columnLast=Math.Min(
				last+1,
				(int)Math.Ceiling((end-wave.Start)/wave.Interval));
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
			double position=area.Left+x;
			drawingContext.DrawLine(
				line,
				new Point(position,Y(minimum)),
				new Point(position,Y(maximum)+0.5));
		}
	}

	private void DrawCursors(DrawingContext drawingContext,Rect area)
	{
		for(int index=0;index < cursorActive.Length;index++)
		{
			if(!cursorActive[index] ||
				cursorTimes[index] is not double time ||
				time < viewTimeMin ||
				time > viewTimeMax)
			{
				continue;
			}
			double x=
				area.Left+
				(time-viewTimeMin)/(viewTimeMax-viewTimeMin)*area.Width;
			SolidColorBrush brush=new(CursorColors[index]);
			Pen pen=new(brush,selectedCursor == index ? 2.5 : 1.5)
			{
				DashStyle=movingCursor == index
					? DashStyles.Dash
					: DashStyles.Solid
			};
			drawingContext.DrawLine(
				pen,
				new Point(x,area.Top),
				new Point(x,area.Bottom));
			drawingContext.DrawText(
				Text("K"+(index+1),brush),
				new Point(x+3,area.Top+3));
		}
	}

	private void DrawCursorInformation(
		DrawingContext drawingContext,
		Rect area)
	{
		List<(string Text,Brush Brush)> lines=[];
		for(int index=0;index < cursorActive.Length;index++)
		{
			if(cursorActive[index] && cursorTimes[index] is double time)
			{
				string marker=movingCursor == index
					? " [RUCH]"
					: selectedCursor == index
						? " [WYBRANY]"
						: "";
				lines.Add((
					CursorValues("K"+(index+1),time)+marker,
					new SolidColorBrush(CursorColors[index])));
			}
		}
		foreach((int first,int second) in ActiveCursorPairs())
		{
			double firstTime=cursorTimes[first-1]!.Value;
			double secondTime=cursorTimes[second-1]!.Value;
			string line=
				$"ΔK{first}-K{second}: Δt="+
				Engineering(secondTime-firstTime,"s");
			foreach(Waveform wave in
				CurrentWaves().OrderBy(item=>item.Channel))
			{
				double? firstVoltage=VoltageAt(wave,firstTime);
				double? secondVoltage=VoltageAt(wave,secondTime);
				if(firstVoltage.HasValue && secondVoltage.HasValue)
				{
					line+=
						$"  ΔCH{wave.Channel}="+
						Engineering(
							secondVoltage.Value-firstVoltage.Value,
							"V");
				}
			}
			lines.Add((line,Brushes.WhiteSmoke));
		}
		if(lines.Count == 0)
		{
			return;
		}
		FormattedText[] textLines=lines
			.Select(line=>Text(line.Text,line.Brush))
			.ToArray();
		double width=textLines.Max(line=>line.Width)+12;
		double height=textLines.Sum(line=>line.Height+2)+8;
		double left=Math.Max(area.Left+5,area.Right-width-5);
		drawingContext.DrawRectangle(
			new SolidColorBrush(Color.FromArgb(215,25,25,25)),
			null,
			new Rect(left,area.Top+5,width,height));
		double top=area.Top+9;
		foreach(FormattedText line in textLines)
		{
			drawingContext.DrawText(line,new Point(left+6,top));
			top+=line.Height+2;
		}
	}

	private FormattedText Text(string value,Brush brush)
	{
		return new FormattedText(
			value,
			CultureInfo.InvariantCulture,
			FlowDirection.LeftToRight,
			new Typeface("Consolas"),
			12,
			brush,
			VisualTreeHelper.GetDpi(this).PixelsPerDip);
	}

	private string CursorValues(string name,double time)
	{
		string result=name+"="+Engineering(time,"s");
		foreach(Waveform wave in
			CurrentWaves().OrderBy(item=>item.Channel))
		{
			double? voltage=VoltageAt(wave,time);
			if(voltage.HasValue)
			{
				result+=
					$"  CH{wave.Channel}="+
					Engineering(voltage.Value,"V");
			}
		}
		return result;
	}

	private static double? VoltageAt(Waveform wave,double time)
	{
		int index=(int)Math.Round((time-wave.Start)/wave.Interval);
		return index >= 0 && index < wave.Volts.Length
			? wave.Volts[index]
			: null;
	}

	private Waveform[] CurrentWaves()=>
		waves.Where(wave=>visibleChannels.Contains(wave.Channel)).ToArray();

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
		fullTimeMax=current.Max(
			wave=>wave.Start+(wave.Volts.Length-1)*wave.Interval);
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
				cursorTimes[index]=Math.Clamp(
					time,
					fullTimeMin,
					fullTimeMax);
			}
		}
	}

	private double FractionAt(double x)
	{
		Rect area=PlotArea;
		return Math.Clamp((x-area.Left)/area.Width,0,1);
	}

	private double TimeAt(double fraction)=>
		viewTimeMin+(viewTimeMax-viewTimeMin)*fraction;

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
		InvalidateVisual();
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
		double scale=
			Math.Max(1,Math.Max(Math.Abs(first),Math.Abs(second)));
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
		return (value/factor).ToString(
			"0.###",
			CultureInfo.InvariantCulture)+" "+prefix+unit;
	}
}
