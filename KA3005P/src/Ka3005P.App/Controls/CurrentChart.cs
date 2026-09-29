using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Ka3005P.App.ViewModels;

namespace Ka3005P.App.Controls;

public sealed class CurrentChart : FrameworkElement
{
	public static readonly DependencyProperty PointsProperty=
		DependencyProperty.Register(
			nameof(Points),
			typeof(IEnumerable<ChartPoint>),
			typeof(CurrentChart),
			new FrameworkPropertyMetadata(null,OnPointsChanged));
	public static readonly DependencyProperty MinimumYProperty=
		RegisterRenderProperty(nameof(MinimumY),0d);
	public static readonly DependencyProperty MaximumYProperty=
		RegisterRenderProperty(nameof(MaximumY),1d);
	public static readonly DependencyProperty MinimumVoltageYProperty=
		RegisterRenderProperty(nameof(MinimumVoltageY),0d);
	public static readonly DependencyProperty MaximumVoltageYProperty=
		RegisterRenderProperty(nameof(MaximumVoltageY),1d);
	public static readonly DependencyProperty ShowCurrentProperty=
		RegisterRenderProperty(nameof(ShowCurrent),true);
	public static readonly DependencyProperty ShowVoltageProperty=
		RegisterRenderProperty(nameof(ShowVoltage),false);

	private static readonly Brush VoltageBrush=
		new SolidColorBrush(Color.FromRgb(0,255,0));
	private static readonly Brush CurrentBrush=
		new SolidColorBrush(Color.FromRgb(255,32,32));
	private static readonly Brush AxisTextBrush=Brushes.White;
	private INotifyCollectionChanged? observedCollection;

	public IEnumerable<ChartPoint>? Points
	{
		get => (IEnumerable<ChartPoint>?)GetValue(PointsProperty);
		set => SetValue(PointsProperty,value);
	}

	public double MinimumY
	{
		get => (double)GetValue(MinimumYProperty);
		set => SetValue(MinimumYProperty,value);
	}

	public double MaximumY
	{
		get => (double)GetValue(MaximumYProperty);
		set => SetValue(MaximumYProperty,value);
	}

	public double MinimumVoltageY
	{
		get => (double)GetValue(MinimumVoltageYProperty);
		set => SetValue(MinimumVoltageYProperty,value);
	}

	public double MaximumVoltageY
	{
		get => (double)GetValue(MaximumVoltageYProperty);
		set => SetValue(MaximumVoltageYProperty,value);
	}

	public bool ShowCurrent
	{
		get => (bool)GetValue(ShowCurrentProperty);
		set => SetValue(ShowCurrentProperty,value);
	}

	public bool ShowVoltage
	{
		get => (bool)GetValue(ShowVoltageProperty);
		set => SetValue(ShowVoltageProperty,value);
	}

	protected override void OnRender(DrawingContext drawingContext)
	{
		base.OnRender(drawingContext);
		Rect area=new(48,8,Math.Max(0,ActualWidth-96),Math.Max(0,ActualHeight-28));
		drawingContext.DrawRectangle(
			new SolidColorBrush(Color.FromRgb(100,100,100)),
			new Pen(Brushes.Black,1),
			area);
		DrawGrid(drawingContext,area);

		ChartPoint[] points=Points?.ToArray() ?? [];
		if(points.Length<2 || area.Width<=0 || area.Height<=0)
		{
			return;
		}
		if(ShowCurrent)
		{
			DrawSeries(
				drawingContext,
				area,
				points,
				point=>point.CurrentAmperes,
				MinimumY,
				MaximumY,
				CurrentBrush);
		}
		if(ShowVoltage)
		{
			DrawSeries(
				drawingContext,
				area,
				points,
				point=>point.VoltageVolts,
				MinimumVoltageY,
				MaximumVoltageY,
				VoltageBrush);
		}
	}

	private void DrawGrid(DrawingContext drawingContext,Rect area)
	{
		Pen gridPen=new(new SolidColorBrush(Color.FromRgb(190,190,190)),0.7);
		for(int index=0;index<=10;index++)
		{
			double y=area.Top+(area.Height*index/10d);
			drawingContext.DrawLine(
				gridPen,
				new Point(area.Left,y),
				new Point(area.Right,y));
			if(index%2 != 0)
			{
				continue;
			}
			if(ShowCurrent)
			{
				double current=MaximumY-((MaximumY-MinimumY)*index/10d);
				DrawLabel(
					drawingContext,
					current.ToString("0.0",CultureInfo.InvariantCulture),
					AxisTextBrush,
					new Point(area.Left-4,y),
					true);
			}
			if(ShowVoltage)
			{
				double voltage=MaximumVoltageY-
					((MaximumVoltageY-MinimumVoltageY)*index/10d);
				DrawLabel(
					drawingContext,
					voltage.ToString("0.0",CultureInfo.InvariantCulture),
					AxisTextBrush,
					new Point(area.Right+4,y),
					false);
			}
		}
	}

	private void DrawLabel(
		DrawingContext drawingContext,
		string value,
		Brush brush,
		Point anchor,
		bool alignRight)
	{
		FormattedText label=new(
			value,
			CultureInfo.InvariantCulture,
			FlowDirection.LeftToRight,
			new Typeface("Consolas"),
			10,
			brush,
			VisualTreeHelper.GetDpi(this).PixelsPerDip);
		double x=alignRight ? Math.Max(0,anchor.X-label.Width) : anchor.X;
		drawingContext.DrawText(label,new Point(x,anchor.Y-label.Height/2));
	}

	private static void DrawSeries(
		DrawingContext drawingContext,
		Rect area,
		IReadOnlyList<ChartPoint> points,
		Func<ChartPoint,double> selector,
		double minimum,
		double maximum,
		Brush brush)
	{
		double range=Math.Max(0.000001,maximum-minimum);
		StreamGeometry geometry=new();
		using(StreamGeometryContext context=geometry.Open())
		{
			for(int index=0;index<points.Count;index++)
			{
				double x=area.Left+(area.Width*index/(points.Count-1d));
				double normalized=(selector(points[index])-minimum)/range;
				double y=area.Bottom-(Math.Clamp(normalized,0,1)*area.Height);
				Point point=new(x,y);
				if(index == 0)
				{
					context.BeginFigure(point,false,false);
				}
				else
				{
					context.LineTo(point,true,false);
				}
			}
		}
		geometry.Freeze();
		drawingContext.DrawGeometry(null,new Pen(brush,2),geometry);
	}

	private static DependencyProperty RegisterRenderProperty(
		string name,
		object defaultValue)
	{
		return DependencyProperty.Register(
			name,
			defaultValue.GetType(),
			typeof(CurrentChart),
			new FrameworkPropertyMetadata(
				defaultValue,
				FrameworkPropertyMetadataOptions.AffectsRender));
	}

	private static void OnPointsChanged(
		DependencyObject dependencyObject,
		DependencyPropertyChangedEventArgs eventArgs)
	{
		CurrentChart chart=(CurrentChart)dependencyObject;
		if(chart.observedCollection is not null)
		{
			chart.observedCollection.CollectionChanged-=chart.OnCollectionChanged;
		}
		chart.observedCollection=eventArgs.NewValue as INotifyCollectionChanged;
		if(chart.observedCollection is not null)
		{
			chart.observedCollection.CollectionChanged+=chart.OnCollectionChanged;
		}
		chart.InvalidateVisual();
	}

	private void OnCollectionChanged(
		object? sender,
		NotifyCollectionChangedEventArgs eventArgs)
	{
		InvalidateVisual();
	}
}
