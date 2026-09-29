using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;
using Ka3005P.App.ViewModels;
using LabStation.UI.Controls;

namespace Ka3005P.App.Controls;

public sealed class CurrentChart : TimeSeriesPlot
{
	public static readonly DependencyProperty PointsProperty=
		DependencyProperty.Register(
			nameof(Points),
			typeof(IEnumerable<ChartPoint>),
			typeof(CurrentChart),
			new FrameworkPropertyMetadata(null,OnPointsChanged));
	public static readonly DependencyProperty MinimumYProperty=
		RegisterRefreshProperty(nameof(MinimumY),0d);
	public static readonly DependencyProperty MaximumYProperty=
		RegisterRefreshProperty(nameof(MaximumY),1d);
	public static readonly DependencyProperty MinimumVoltageYProperty=
		RegisterRefreshProperty(nameof(MinimumVoltageY),0d);
	public static readonly DependencyProperty MaximumVoltageYProperty=
		RegisterRefreshProperty(nameof(MaximumVoltageY),1d);
	public static readonly DependencyProperty ShowCurrentProperty=
		RegisterRefreshProperty(nameof(ShowCurrent),true);
	public static readonly DependencyProperty ShowVoltageProperty=
		RegisterRefreshProperty(nameof(ShowVoltage),false);

	private INotifyCollectionChanged? observedCollection;

	public CurrentChart()
	{
		CursorCount=2;
		HorizontalTitle="Czas";
		HorizontalUnit="s";
		EmptyMessage="Brak danych pomiarowych";
	}

	public IEnumerable<ChartPoint>? Points
	{
		get=>(IEnumerable<ChartPoint>?)GetValue(PointsProperty);
		set=>SetValue(PointsProperty,value);
	}

	public double MinimumY
	{
		get=>(double)GetValue(MinimumYProperty);
		set=>SetValue(MinimumYProperty,value);
	}

	public double MaximumY
	{
		get=>(double)GetValue(MaximumYProperty);
		set=>SetValue(MaximumYProperty,value);
	}

	public double MinimumVoltageY
	{
		get=>(double)GetValue(MinimumVoltageYProperty);
		set=>SetValue(MinimumVoltageYProperty,value);
	}

	public double MaximumVoltageY
	{
		get=>(double)GetValue(MaximumVoltageYProperty);
		set=>SetValue(MaximumVoltageYProperty,value);
	}

	public bool ShowCurrent
	{
		get=>(bool)GetValue(ShowCurrentProperty);
		set=>SetValue(ShowCurrentProperty,value);
	}

	public bool ShowVoltage
	{
		get=>(bool)GetValue(ShowVoltageProperty);
		set=>SetValue(ShowVoltageProperty,value);
	}

	private void RefreshSeries()
	{
		ChartPoint[] points=Points?.ToArray() ?? [];
		bool symmetric=points.LastOrDefault().IsSymmetric;
		List<PlotSeries> visible=[];
		if(ShowCurrent)
		{
			if(symmetric)
			{
				visible.Add(CreateSeries("P1 I","A",Colors.Red,PlotAxis.Left,points,point=>point.FirstCurrentAmperes));
				visible.Add(CreateSeries("P2 I","A",Colors.Orange,PlotAxis.Left,points,point=>point.SecondCurrentAmperes));
			}
			else
			{
				visible.Add(CreateSeries("I","A",Colors.Red,PlotAxis.Left,points,point=>point.CurrentAmperes));
			}
		}
		if(ShowVoltage)
		{
			if(symmetric)
			{
				visible.Add(CreateSeries("P1 U","V",Colors.Lime,PlotAxis.Right,points,point=>point.FirstVoltageVolts));
				visible.Add(CreateSeries("P2 U","V",Colors.Cyan,PlotAxis.Right,points,point=>point.SecondVoltageVolts));
			}
			else
			{
				visible.Add(CreateSeries("U","V",Colors.Lime,PlotAxis.Right,points,point=>point.VoltageVolts));
			}
		}
		SetSeries(visible);
	}

	private static PlotSeries CreateSeries(
		string name,
		string unit,
		Color color,
		PlotAxis axis,
		IEnumerable<ChartPoint> points,
		Func<ChartPoint,double> value)
	{
		return new PlotSeries(
			name,
			unit,
			color,
			axis,
			points.Select(point=>new PlotPoint(
				point.Elapsed.TotalSeconds,
				value(point))).ToArray());
	}

	private static DependencyProperty RegisterRefreshProperty(
		string name,
		object defaultValue)
	{
		return DependencyProperty.Register(
			name,
			defaultValue.GetType(),
			typeof(CurrentChart),
			new FrameworkPropertyMetadata(defaultValue,OnRenderPropertyChanged));
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
		chart.RefreshSeries();
	}

	private static void OnRenderPropertyChanged(
		DependencyObject dependencyObject,
		DependencyPropertyChangedEventArgs eventArgs)
	{
		((CurrentChart)dependencyObject).RefreshSeries();
	}

	private void OnCollectionChanged(
		object? sender,
		NotifyCollectionChangedEventArgs eventArgs)
	{
		RefreshSeries();
	}
}
