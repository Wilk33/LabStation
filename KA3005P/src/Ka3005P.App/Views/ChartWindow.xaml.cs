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
		Chart.CursorStateChanged+=OnCursorStateChanged;
		UpdateCursorButtons();
	}

	private void Cursor1Click(object sender,RoutedEventArgs eventArgs)=>
		Chart.ActivateOrSelectCursor(0);

	private void Cursor2Click(object sender,RoutedEventArgs eventArgs)=>
		Chart.ActivateOrSelectCursor(1);

	private void OnCursorStateChanged(object? sender,EventArgs eventArgs)
	{
		UpdateCursorButtons();
	}

	private void UpdateCursorButtons()
	{
		System.Windows.Controls.Button[] buttons=
		[
			Cursor1Button,
			Cursor2Button
		];
		for(int index=0;index<buttons.Length;index++)
		{
			CursorButtonVisual.Apply(
				buttons[index],
				Chart.IsCursorActive(index),
				new System.Windows.Media.SolidColorBrush(
					TimeSeriesPlot.CursorColor(index)),
				(System.Windows.Media.Brush)FindResource("KoradInputBrush"));
		}
	}

	protected override void OnClosed(EventArgs eventArgs)
	{
		Chart.CursorStateChanged-=OnCursorStateChanged;
		if(DataContext is ChartViewModel viewModel)
		{
			viewModel.Dispose();
		}
		base.OnClosed(eventArgs);
	}
}
