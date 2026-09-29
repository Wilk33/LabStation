using System.Windows.Controls;
using Ka3005P.App.ViewModels;
using Ka3005P.Core.Dual;

namespace Ka3005P.App.Views;

public partial class DualSupplyView : UserControl
{
	public DualSupplyView()
	{
		InitializeComponent();
	}

	private async void ModeSelectionChanged(
		object sender,
		SelectionChangedEventArgs eventArgs)
	{
		if(DataContext is not DualSupplyViewModel viewModel ||
			sender is not ComboBox comboBox ||
			comboBox.SelectedValue is not DualMode mode)
		{
			return;
		}
		await viewModel.ChangeModeCommand.ExecuteAsync(mode);
	}
}
