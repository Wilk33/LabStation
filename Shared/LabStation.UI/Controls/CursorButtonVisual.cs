using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LabStation.UI.Controls;

public static class CursorButtonVisual
{
	private static readonly DependencyProperty ActiveProperty=
		DependencyProperty.RegisterAttached(
			"Active",
			typeof(bool),
			typeof(CursorButtonVisual));
	private static readonly DependencyProperty ActiveBackgroundProperty=
		DependencyProperty.RegisterAttached(
			"ActiveBackground",
			typeof(Brush),
			typeof(CursorButtonVisual));
	private static readonly DependencyProperty InactiveBackgroundProperty=
		DependencyProperty.RegisterAttached(
			"InactiveBackground",
			typeof(Brush),
			typeof(CursorButtonVisual));
	private static readonly DependencyProperty HandlersAttachedProperty=
		DependencyProperty.RegisterAttached(
			"HandlersAttached",
			typeof(bool),
			typeof(CursorButtonVisual));
	private static readonly DependencyProperty HoveringProperty=
		DependencyProperty.RegisterAttached(
			"Hovering",
			typeof(bool),
			typeof(CursorButtonVisual));

	public static void Apply(
		Button button,
		bool active,
		Brush activeBackground,
		Brush inactiveBackground)
	{
		ArgumentNullException.ThrowIfNull(button);
		ArgumentNullException.ThrowIfNull(activeBackground);
		ArgumentNullException.ThrowIfNull(inactiveBackground);
		button.SetValue(ActiveProperty,active);
		button.SetValue(ActiveBackgroundProperty,activeBackground);
		button.SetValue(InactiveBackgroundProperty,inactiveBackground);
		if(!(bool)button.GetValue(HandlersAttachedProperty))
		{
			button.MouseEnter+=ButtonMouseEnter;
			button.MouseLeave+=ButtonMouseLeave;
			button.SetValue(HandlersAttachedProperty,true);
		}
		Restore(button);
	}

	private static void ButtonMouseEnter(object sender,MouseEventArgs eventArgs)
	{
		if(sender is Button button)
		{
			button.SetValue(HoveringProperty,true);
			button.Foreground=Brushes.White;
		}
	}

	private static void ButtonMouseLeave(object sender,MouseEventArgs eventArgs)
	{
		if(sender is Button button)
		{
			button.SetValue(HoveringProperty,false);
			Restore(button);
		}
	}

	private static void Restore(Button button)
	{
		bool active=(bool)button.GetValue(ActiveProperty);
		bool hovering=(bool)button.GetValue(HoveringProperty);
		button.Background=(Brush)button.GetValue(
			active ? ActiveBackgroundProperty : InactiveBackgroundProperty);
		button.Foreground=active && !hovering ? Brushes.Black : Brushes.White;
	}
}
