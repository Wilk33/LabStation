using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LabStation.UI.Controls;

public sealed class EngineeringMultiplierChangedEventArgs(double multiplier) : EventArgs
{
	public double Multiplier { get; }=multiplier;
}

public partial class EngineeringStepSelector : UserControl
{
	private readonly Dictionary<Button,double> multipliers;

	public EngineeringStepSelector()
	{
		InitializeComponent();
		multipliers=new()
		{
			[GigaButton]=1_000_000_000,
			[MegaButton]=1_000_000,
			[KiloButton]=1_000,
			[OneButton]=1,
			[MilliButton]=0.001,
			[MicroButton]=0.000001,
			[NanoButton]=0.000000001
		};
		foreach((Button button,double multiplier) in multipliers)
		{
			button.Tag=multiplier;
		}
		ApplyVisualState();
	}

	public event EventHandler<EngineeringMultiplierChangedEventArgs>? MultiplierChanged;

	public double SelectedMultiplier { get; private set; }=0.001;

	private void MultiplierClick(object sender,RoutedEventArgs eventArgs)
	{
		if(sender is not Button button ||
			!multipliers.TryGetValue(button,out double multiplier))
		{
			return;
		}
		SelectedMultiplier=multiplier;
		ApplyVisualState();
		MultiplierChanged?.Invoke(this,new(multiplier));
	}

	private void ApplyVisualState()
	{
		foreach((Button button,double multiplier) in multipliers)
		{
			if(multiplier == SelectedMultiplier)
			{
				button.Background=(Brush)FindResource("LabStationOnActiveBrush");
				button.Foreground=Brushes.Black;
			}
			else
			{
				button.Background=(Brush)FindResource("LabStationInputBrush");
				button.Foreground=(Brush)FindResource("LabStationTextBrush");
			}
		}
	}
}
