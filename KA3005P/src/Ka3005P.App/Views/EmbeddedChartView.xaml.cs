using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ka3005P.App.ViewModels;
using LabStation.UI.Controls;

namespace Ka3005P.App.Views;

public partial class EmbeddedChartView : UserControl
{
	public EmbeddedChartView()
	{
		InitializeComponent();
		Chart.CursorStateChanged+=OnCursorStateChanged;
		Unloaded+=OnUnloaded;
		UpdateCursorButtons();
	}

	private void Cursor1Click(object sender,RoutedEventArgs eventArgs)=>
		Chart.ActivateOrSelectCursor(0);

	private void Cursor2Click(object sender,RoutedEventArgs eventArgs)=>
		Chart.ActivateOrSelectCursor(1);

	private void OnCursorStateChanged(object? sender,EventArgs eventArgs)=>
		UpdateCursorButtons();

	private void UpdateCursorButtons()
	{
		Button[] buttons=[Cursor1Button,Cursor2Button];
		for(int index=0;index<buttons.Length;index++)
		{
			CursorButtonVisual.Apply(
				buttons[index],
				Chart.IsCursorActive(index),
				new SolidColorBrush(TimeSeriesPlot.CursorColor(index)),
				(Brush)FindResource("KoradInputBrush"));
		}
	}

	private void OnUnloaded(object sender,RoutedEventArgs eventArgs)
	{
		Chart.CursorStateChanged-=OnCursorStateChanged;
		Unloaded-=OnUnloaded;
	}
}
