using System.Windows.Controls;
using LabStation.UI.Controls;
using Ka3005P.App.ViewModels;

namespace Ka3005P.App.Views;

public partial class SingleSupplyView : UserControl
{
	public SingleSupplyView()
	{
		InitializeComponent();
	}

	private void StepMultiplierChanged(
		object sender,
		EngineeringMultiplierChangedEventArgs eventArgs)
	{
		if(DataContext is SingleSupplyViewModel viewModel)
		{
			viewModel.SetStepMultiplier(eventArgs.Multiplier);
		}
	}
}
