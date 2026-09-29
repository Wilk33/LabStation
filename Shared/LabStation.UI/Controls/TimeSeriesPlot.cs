using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LabStation.UI.Controls;

public enum PlotAxis
{
	Left,
	Right
}

public readonly record struct PlotPoint(double X,double Y);

public sealed record PlotSeries(
	string Name,
	string Unit,
	Color Color,
	PlotAxis Axis,
	IReadOnlyList<PlotPoint> Points);

public class TimeSeriesPlot : FrameworkElement
{
	public static readonly DependencyProperty CursorCountProperty=
		DependencyProperty.Register(
			nameof(CursorCount),
			typeof(int),
			typeof(TimeSeriesPlot),
			new FrameworkPropertyMetadata(0,OnCursorCountChanged));
	public static readonly DependencyProperty CursorsEnabledProperty=
		DependencyProperty.Register(
			nameof(CursorsEnabled),
			typeof(bool),
			typeof(TimeSeriesPlot),
			new FrameworkPropertyMetadata(false,OnCursorsEnabledChanged));

	private static readonly Color[] CursorColors=
	[
		Colors.OrangeRed,
		Colors.LimeGreen,
		Colors.DeepSkyBlue,
		Colors.Magenta
	];
	private readonly bool[] cursorActive=new bool[4];
	private readonly double?[] cursorPositions=new double?[4];
	private PlotSeries[] series=[];
	private Point? hover;
	private double fullXMin;
	private double fullXMax=1;
	private double viewXMin;
	private double viewXMax=1;
	private int selectedCursor=-1;
	private int movingCursor=-1;
	private bool stale;

	public TimeSeriesPlot()
	{
		Focusable=true;
		ClipToBounds=true;
	}

	public event EventHandler? CursorStateChanged;

	public int CursorCount
	{
		get=>(int)GetValue(CursorCountProperty);
		set=>SetValue(CursorCountProperty,value);
	}

	public bool CursorsEnabled
	{
		get=>(bool)GetValue(CursorsEnabledProperty);
		set=>SetValue(CursorsEnabledProperty,value);
	}

	public bool Stale
	{
		get=>stale;
		set
		{
			if(stale == value)
			{
				return;
			}
			stale=value;
			InvalidateVisual();
		}
	}

	public string EmptyMessage { get;set; }=string.Empty;
	public string HorizontalTitle { get;set; }="Czas";
	public string HorizontalUnit { get;set; }="s";
	public string StaleMessage { get;set; }="DANE NIEAKTUALNE / OFFLINE";
	public bool HasSeries=>series.Any(item=>item.Points.Count>0);
	public int SelectedCursor=>selectedCursor;
	public int MovingCursor=>movingCursor;
	public (double Min,double Max) VisibleRange=>(viewXMin,viewXMax);
	public IReadOnlyList<PlotSeries> Series=>series;

	public void SetSeries(IEnumerable<PlotSeries> value)
	{
		ArgumentNullException.ThrowIfNull(value);
		bool wasFull=
			!HasSeries ||
			Nearly(viewXMin,fullXMin) && Nearly(viewXMax,fullXMax);
		series=value
			.Where(item=>item.Points.Count>0)
			.ToArray();
		UpdateRange(wasFull);
		InvalidateVisual();
	}

	public void ZoomAt(double fraction,int wheelDelta)
	{
		if(!HasSeries || wheelDelta == 0)
		{
			return;
		}
		fraction=Math.Clamp(fraction,0,1);
		double fullSpan=fullXMax-fullXMin;
		double span=viewXMax-viewXMin;
		double factor=wheelDelta>0 ? 0.8 : 1.25;
		double newSpan=Math.Clamp(span*factor,fullSpan/10000,fullSpan);
		double anchor=viewXMin+span*fraction;
		viewXMin=anchor-newSpan*fraction;
		viewXMax=viewXMin+newSpan;
		ClampView();
		InvalidateVisual();
	}

	public void ResetZoom()
	{
		viewXMin=fullXMin;
		viewXMax=fullXMax;
		InvalidateVisual();
	}

	public void ActivateOrSelectCursor(int index)
	{
		ValidateCursor(index);
		if(!CursorsEnabled || !HasSeries)
		{
			return;
		}
		if(cursorActive[index] && selectedCursor == index && movingCursor != index)
		{
			cursorActive[index]=false;
			cursorPositions[index]=null;
			selectedCursor=Enumerable.Range(0,CursorCount)
				.FirstOrDefault(cursor=>cursorActive[cursor],-1);
		}
		else
		{
			cursorActive[index]=true;
			selectedCursor=index;
			cursorPositions[index]??=
				viewXMin+(viewXMax-viewXMin)*(index+1)/(CursorCount+1);
		}
		NotifyCursorStateChanged();
	}

	public void ClearCursors()
	{
		Array.Fill(cursorActive,false);
		Array.Fill(cursorPositions,null);
		selectedCursor=-1;
		movingCursor=-1;
		ReleaseMouseCapture();
		NotifyCursorStateChanged();
	}

	public static Color CursorColor(int index)
	{
		if(index<0 || index>=CursorColors.Length)
		{
			throw new ArgumentOutOfRangeException(nameof(index));
		}
		return CursorColors[index];
	}

	public bool IsCursorActive(int index)
	{
		ValidateCursor(index);
		return cursorActive[index];
	}

	public double? CursorPosition(int index)
	{
		ValidateCursor(index);
		return cursorPositions[index];
	}

	public (int First,int Second)[] ActiveCursorPairs()
	{
		int[] active=Enumerable.Range(0,CursorCount)
			.Where(index=>cursorActive[index])
			.ToArray();
		return Enumerable.Range(0,active.Length/2)
			.Select(pair=>(active[pair*2]+1,active[pair*2+1]+1))
			.ToArray();
	}

	public void UnlockSelectedCursor(double fraction)
	{
		if(!CursorsEnabled ||
			selectedCursor<0 ||
			!cursorActive[selectedCursor] ||
			!HasSeries)
		{
			return;
		}
		movingCursor=selectedCursor;
		MoveUnlockedCursor(fraction);
		NotifyCursorStateChanged();
	}

	public void MoveUnlockedCursor(double fraction)
	{
		if(movingCursor<0 || !HasSeries)
		{
			return;
		}
		fraction=Math.Clamp(fraction,0,1);
		cursorPositions[movingCursor]=viewXMin+(viewXMax-viewXMin)*fraction;
		NotifyCursorStateChanged();
	}

	public void PlaceUnlockedCursor(double fraction)
	{
		if(movingCursor<0)
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
		if(movingCursor>=0)
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
		if(movingCursor<0)
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
			if(movingCursor>=0)
			{
				CaptureMouse();
			}
		}
		else if(eventArgs.ChangedButton == MouseButton.Left && movingCursor>=0)
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
		if(area.Width<20 || area.Height<20)
		{
			return;
		}
		DrawGrid(drawingContext,area);
		if(!HasSeries)
		{
			DrawEmptyMessage(drawingContext,area);
			return;
		}
		(PlotSeries[] left,PlotSeries[] right)=AxisSeries();
		(double leftMin,double leftMax)=Range(left);
		(double rightMin,double rightMax)=Range(right);
		DrawAxisLabels(drawingContext,area,left,leftMin,leftMax,true);
		DrawAxisLabels(drawingContext,area,right,rightMin,rightMax,false);
		DrawHorizontalLabels(drawingContext,area);
		drawingContext.PushClip(new RectangleGeometry(area));
		foreach(PlotSeries item in series)
		{
			(double minimum,double maximum)=item.Axis == PlotAxis.Left
				? (leftMin,leftMax)
				: (rightMin,rightMax);
			DrawSeries(drawingContext,area,item,minimum,maximum);
		}
		DrawCursors(drawingContext,area);
		if(hover is Point hoverPoint && area.Contains(hoverPoint))
		{
			Pen cross=new(new SolidColorBrush(Color.FromRgb(160,160,160)),1)
			{
				DashStyle=DashStyles.Dot
			};
			drawingContext.DrawLine(cross,new Point(hoverPoint.X,area.Top),new Point(hoverPoint.X,area.Bottom));
		}
		drawingContext.Pop();
		drawingContext.DrawText(
			Text(HorizontalTitle,Brushes.WhiteSmoke),
			new Point(area.Right-40,area.Bottom+32));
		if(hover is Point point && area.Contains(point))
		{
			double x=PositionAt(FractionAt(point.X));
			drawingContext.DrawText(
				Text(CursorValues("x",x),Brushes.WhiteSmoke),
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
				Text(StaleMessage,Brushes.WhiteSmoke),
				new Point(area.Left+15,area.Top+14));
		}
	}

	private Rect PlotArea=>new(
		78,
		28,
		Math.Max(1,ActualWidth-156),
		Math.Max(1,ActualHeight-86));

	private void DrawGrid(DrawingContext drawingContext,Rect area)
	{
		Pen grid=new(new SolidColorBrush(Color.FromRgb(75,75,75)),1);
		for(int index=0;index<=10;index++)
		{
			double x=area.Left+area.Width*index/10;
			drawingContext.DrawLine(grid,new Point(x,area.Top),new Point(x,area.Bottom));
		}
		for(int index=0;index<=8;index++)
		{
			double y=area.Top+area.Height*index/8;
			drawingContext.DrawLine(grid,new Point(area.Left,y),new Point(area.Right,y));
		}
	}

	private void DrawEmptyMessage(DrawingContext drawingContext,Rect area)
	{
		if(string.IsNullOrWhiteSpace(EmptyMessage))
		{
			return;
		}
		FormattedText text=Text(EmptyMessage,Brushes.WhiteSmoke);
		drawingContext.DrawText(
			text,
			new Point(
				area.Left+(area.Width-text.Width)/2,
				area.Top+(area.Height-text.Height)/2));
	}

	private void DrawAxisLabels(
		DrawingContext drawingContext,
		Rect area,
		PlotSeries[] items,
		double minimum,
		double maximum,
		bool left)
	{
		if(items.Length == 0)
		{
			return;
		}
		string unit=items.Select(item=>item.Unit).Distinct().Count() == 1
			? items[0].Unit
			: string.Empty;
		for(int index=0;index<=8;index++)
		{
			double value=maximum-(maximum-minimum)*index/8;
			FormattedText label=Text(Engineering(value,unit),Brushes.WhiteSmoke);
			double x=left ? Math.Max(0,area.Left-label.Width-7) : area.Right+7;
			drawingContext.DrawText(label,new Point(x,area.Top+area.Height*index/8-8));
		}
	}

	private void DrawHorizontalLabels(DrawingContext drawingContext,Rect area)
	{
		for(int index=0;index<=4;index++)
		{
			FormattedText label=Text(
				Engineering(viewXMin+(viewXMax-viewXMin)*index/4,HorizontalUnit),
				Brushes.WhiteSmoke);
			double labelX=Math.Clamp(
				area.Left+area.Width*index/4-label.Width/2,
				0,
				Math.Max(0,ActualWidth-label.Width-4));
			drawingContext.DrawText(label,new Point(labelX,area.Bottom+8));
		}
	}

	private void DrawSeries(
		DrawingContext drawingContext,
		Rect area,
		PlotSeries item,
		double minimum,
		double maximum)
	{
		PlotPoint[] visible=item.Points
			.Where(point=>point.X>=viewXMin && point.X<=viewXMax)
			.ToArray();
		if(visible.Length == 0)
		{
			return;
		}
		Pen line=new(new SolidColorBrush(item.Color),1.25);
		double X(double value)=>area.Left+(value-viewXMin)/(viewXMax-viewXMin)*area.Width;
		double Y(double value)=>area.Bottom-(value-minimum)/(maximum-minimum)*area.Height;
		if(visible.Length<=area.Width*2)
		{
			StreamGeometry geometry=new();
			using(StreamGeometryContext context=geometry.Open())
			{
				context.BeginFigure(new Point(X(visible[0].X),Y(visible[0].Y)),false,false);
				if(visible.Length>1)
				{
					context.PolyLineTo(
						visible.Skip(1).Select(point=>new Point(X(point.X),Y(point.Y))).ToArray(),
						true,
						false);
				}
			}
			drawingContext.DrawGeometry(null,line,geometry);
			return;
		}
		int columnCount=Math.Max(1,(int)Math.Ceiling(area.Width));
		double[] minimums=Enumerable.Repeat(double.PositiveInfinity,columnCount).ToArray();
		double[] maximums=Enumerable.Repeat(double.NegativeInfinity,columnCount).ToArray();
		foreach(PlotPoint point in visible)
		{
			int column=(int)((point.X-viewXMin)/(viewXMax-viewXMin)*columnCount);
			column=Math.Clamp(column,0,columnCount-1);
			minimums[column]=Math.Min(minimums[column],point.Y);
			maximums[column]=Math.Max(maximums[column],point.Y);
		}
		for(int column=0;column<columnCount;column++)
		{
			if(double.IsPositiveInfinity(minimums[column]))
			{
				continue;
			}
			double x=area.Left+column;
			drawingContext.DrawLine(
				line,
				new Point(x,Y(minimums[column])),
				new Point(x,Y(maximums[column])+0.5));
		}
	}

	private void DrawCursors(DrawingContext drawingContext,Rect area)
	{
		for(int index=0;index<CursorCount;index++)
		{
			if(!cursorActive[index] ||
				cursorPositions[index] is not double position ||
				position<viewXMin ||
				position>viewXMax)
			{
				continue;
			}
			double x=area.Left+(position-viewXMin)/(viewXMax-viewXMin)*area.Width;
			SolidColorBrush brush=new(CursorColors[index]);
			Pen pen=new(brush,selectedCursor == index ? 2.5 : 1.5)
			{
				DashStyle=movingCursor == index ? DashStyles.Dash : DashStyles.Solid
			};
			drawingContext.DrawLine(pen,new Point(x,area.Top),new Point(x,area.Bottom));
			drawingContext.DrawText(Text("K"+(index+1),brush),new Point(x+3,area.Top+3));
		}
	}

	private void DrawCursorInformation(DrawingContext drawingContext,Rect area)
	{
		List<(string Text,Brush Brush)> lines=[];
		for(int index=0;index<CursorCount;index++)
		{
			if(cursorActive[index] && cursorPositions[index] is double position)
			{
				string marker=movingCursor == index
					? " [RUCH]"
					: selectedCursor == index ? " [WYBRANY]" : string.Empty;
				lines.Add((CursorValues("K"+(index+1),position)+marker,new SolidColorBrush(CursorColors[index])));
			}
		}
		foreach((int first,int second) in ActiveCursorPairs())
		{
			double firstPosition=cursorPositions[first-1]!.Value;
			double secondPosition=cursorPositions[second-1]!.Value;
			string line=$"ΔK{first}-K{second}: Δx="+Engineering(secondPosition-firstPosition,HorizontalUnit);
			foreach(PlotSeries item in series)
			{
				double? firstValue=ValueAt(item,firstPosition);
				double? secondValue=ValueAt(item,secondPosition);
				if(firstValue.HasValue && secondValue.HasValue)
				{
					line+=$"  Δ{item.Name}="+Engineering(secondValue.Value-firstValue.Value,item.Unit);
				}
			}
			lines.Add((line,Brushes.WhiteSmoke));
		}
		if(lines.Count == 0)
		{
			return;
		}
		FormattedText[] textLines=lines.Select(line=>Text(line.Text,line.Brush)).ToArray();
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

	private string CursorValues(string name,double position)
	{
		string result=name+"="+Engineering(position,HorizontalUnit);
		foreach(PlotSeries item in series)
		{
			double? value=ValueAt(item,position);
			if(value.HasValue)
			{
				result+=$"  {item.Name}="+Engineering(value.Value,item.Unit);
			}
		}
		return result;
	}

	private FormattedText Text(string value,Brush brush)=>new(
		value,
		CultureInfo.InvariantCulture,
		FlowDirection.LeftToRight,
		new Typeface("Consolas"),
		12,
		brush,
		VisualTreeHelper.GetDpi(this).PixelsPerDip);

	private (PlotSeries[] Left,PlotSeries[] Right) AxisSeries()=>
		(series.Where(item=>item.Axis == PlotAxis.Left).ToArray(),
		series.Where(item=>item.Axis == PlotAxis.Right).ToArray());

	private static (double Minimum,double Maximum) Range(PlotSeries[] items)
	{
		if(items.Length == 0)
		{
			return (0,1);
		}
		double minimum=items.Min(item=>item.Points.Min(point=>point.Y));
		double maximum=items.Max(item=>item.Points.Max(point=>point.Y));
		double margin=Math.Max((maximum-minimum)*0.12,Math.Max(Math.Abs(maximum),1)*0.01);
		return (minimum-margin,maximum+margin);
	}

	private static double? ValueAt(PlotSeries item,double position)
	{
		if(item.Points.Count == 0 ||
			position<item.Points[0].X ||
			position>item.Points[^1].X)
		{
			return null;
		}
		int low=0;
		int high=item.Points.Count-1;
		while(low<high)
		{
			int middle=(low+high)/2;
			if(item.Points[middle].X<position)
			{
				low=middle+1;
			}
			else
			{
				high=middle;
			}
		}
		if(low>0 && Math.Abs(item.Points[low-1].X-position)<Math.Abs(item.Points[low].X-position))
		{
			low--;
		}
		return item.Points[low].Y;
	}

	private void UpdateRange(bool resetView)
	{
		if(!HasSeries)
		{
			fullXMin=0;
			fullXMax=1;
			viewXMin=0;
			viewXMax=1;
			return;
		}
		fullXMin=series.Min(item=>item.Points.Min(point=>point.X));
		fullXMax=series.Max(item=>item.Points.Max(point=>point.X));
		if(fullXMax<=fullXMin)
		{
			fullXMax=fullXMin+1;
		}
		if(resetView)
		{
			viewXMin=fullXMin;
			viewXMax=fullXMax;
		}
		else
		{
			ClampView();
		}
		for(int index=0;index<cursorPositions.Length;index++)
		{
			if(cursorPositions[index] is double position)
			{
				cursorPositions[index]=Math.Clamp(position,fullXMin,fullXMax);
			}
		}
	}

	private double FractionAt(double x)=>Math.Clamp((x-PlotArea.Left)/PlotArea.Width,0,1);
	private double PositionAt(double fraction)=>viewXMin+(viewXMax-viewXMin)*fraction;

	private void ClampView()
	{
		double fullSpan=fullXMax-fullXMin;
		double span=Math.Min(viewXMax-viewXMin,fullSpan);
		if(viewXMin<fullXMin)
		{
			viewXMin=fullXMin;
			viewXMax=viewXMin+span;
		}
		if(viewXMax>fullXMax)
		{
			viewXMax=fullXMax;
			viewXMin=viewXMax-span;
		}
	}

	private void ValidateCursor(int index)
	{
		if(index<0 || index>=CursorCount)
		{
			throw new ArgumentOutOfRangeException(nameof(index));
		}
	}

	private void NotifyCursorStateChanged()
	{
		InvalidateVisual();
		CursorStateChanged?.Invoke(this,EventArgs.Empty);
	}

	private static void OnCursorCountChanged(DependencyObject sender,DependencyPropertyChangedEventArgs eventArgs)
	{
		TimeSeriesPlot plot=(TimeSeriesPlot)sender;
		int count=(int)eventArgs.NewValue;
		if(count is <0 or >4)
		{
			throw new ArgumentOutOfRangeException(nameof(CursorCount));
		}
		plot.ClearCursors();
	}

	private static void OnCursorsEnabledChanged(DependencyObject sender,DependencyPropertyChangedEventArgs eventArgs)
	{
		TimeSeriesPlot plot=(TimeSeriesPlot)sender;
		if(!(bool)eventArgs.NewValue)
		{
			plot.ClearCursors();
		}
	}

	private static bool Nearly(double first,double second)
	{
		double scale=Math.Max(1,Math.Max(Math.Abs(first),Math.Abs(second)));
		return Math.Abs(first-second)<=scale*1e-12;
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
		return (value/factor).ToString("0.###",CultureInfo.InvariantCulture)+" "+prefix+unit;
	}
}
