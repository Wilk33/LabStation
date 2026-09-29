using System.Windows;
using LabStation.UI;
using Ka3005P.App.ViewModels;
using LabStation.UI.Controls;

namespace Ka3005P.App.Views;

public partial class ChartWindow : Window
{
	public ChartWindow()
	{
		InitializeComponent();
		SystemTheme.ApplyTo(this);
		Title="Wykres - "+AppInformation.DisplayName;
		Cursor1Button.Background=new System.Windows.Media.SolidColorBrush(
			TimeSeriesPlot.CursorColor(0));
		Cursor2Button.Background=new System.Windows.Media.SolidColorBrush(
			TimeSeriesPlot.CursorColor(1));
	}

	private void Cursor1Click(object sender,RoutedEventArgs eventArgs)=>
		Chart.ActivateOrSelectCursor(0);

	private void Cursor2Click(object sender,RoutedEventArgs eventArgs)=>
		Chart.ActivateOrSelectCursor(1);

	protected override void OnClosed(EventArgs eventArgs)
	{
		if(DataContext is ChartViewModel viewModel)
		{
			viewModel.Dispose();
		}
		base.OnClosed(eventArgs);
	}
}
