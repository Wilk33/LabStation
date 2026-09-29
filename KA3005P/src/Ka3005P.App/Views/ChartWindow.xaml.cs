using System.Windows;
using Ka3005P.App.ViewModels;

namespace Ka3005P.App.Views;

public partial class ChartWindow : Window
{
	public ChartWindow()
	{
		InitializeComponent();
		SystemTheme.ApplyTo(this);
		Title="Wykres - "+AppInformation.DisplayName;
	}

	protected override void OnClosed(EventArgs eventArgs)
	{
		if(DataContext is ChartViewModel viewModel)
		{
			viewModel.Dispose();
		}
		base.OnClosed(eventArgs);
	}
}
