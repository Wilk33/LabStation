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
	public static readonly DependencyProperty VisibleMultipliersProperty=
		DependencyProperty.Register(
			nameof(VisibleMultipliers),
			typeof(string),
			typeof(EngineeringStepSelector),
			new FrameworkPropertyMetadata(
				"G,M,k,1,m,u,n",
				OnVisibleMultipliersChanged));

	private readonly Dictionary<Button,double> multipliers;
	private readonly HashSet<Button> hoveringButtons=[];

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
			button.MouseEnter+=ButtonMouseEnter;
			button.MouseLeave+=ButtonMouseLeave;
		}
		ApplyVisibleMultipliers();
		ApplyVisualState();
	}

	public event EventHandler<EngineeringMultiplierChangedEventArgs>? MultiplierChanged;

	public double SelectedMultiplier { get; private set; }=0.001;

	public string VisibleMultipliers
	{
		get=>(string)GetValue(VisibleMultipliersProperty);
		set=>SetValue(VisibleMultipliersProperty,value);
	}

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
			ApplyButtonVisualState(button,multiplier);
		}
	}

	private void ApplyButtonVisualState(Button button,double multiplier)
	{
		if(multiplier == SelectedMultiplier)
		{
			button.Background=(Brush)FindResource("LabStationOnActiveBrush");
			button.Foreground=hoveringButtons.Contains(button)
				? Brushes.White
				: Brushes.Black;
		}
		else
		{
			button.Background=(Brush)FindResource("LabStationInputBrush");
			button.Foreground=(Brush)FindResource("LabStationTextBrush");
		}
	}

	private void ButtonMouseEnter(object sender,System.Windows.Input.MouseEventArgs eventArgs)
	{
		if(sender is Button button)
		{
			hoveringButtons.Add(button);
			button.Foreground=Brushes.White;
		}
	}

	private void ButtonMouseLeave(object sender,System.Windows.Input.MouseEventArgs eventArgs)
	{
		if(sender is Button button &&
			multipliers.TryGetValue(button,out double multiplier))
		{
			hoveringButtons.Remove(button);
			ApplyButtonVisualState(button,multiplier);
		}
	}

	private static void OnVisibleMultipliersChanged(
		DependencyObject sender,
		DependencyPropertyChangedEventArgs eventArgs)
	{
		((EngineeringStepSelector)sender).ApplyVisibleMultipliers();
	}

	private void ApplyVisibleMultipliers()
	{
		if(multipliers is null)
		{
			return;
		}
		HashSet<string> visible=VisibleMultipliers.Split(
			',',
			StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)
			.ToHashSet(StringComparer.Ordinal);
		int visibleCount=0;
		foreach(Button button in multipliers.Keys)
		{
			bool show=visible.Contains(button.Content?.ToString() ?? string.Empty);
			button.Visibility=show ? Visibility.Visible : Visibility.Collapsed;
			if(show)
			{
				visibleCount++;
			}
		}
		ButtonPanel.Columns=Math.Max(1,visibleCount);
	}
}
