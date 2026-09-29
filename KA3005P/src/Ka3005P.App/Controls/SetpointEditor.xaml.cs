using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ka3005P.App.Controls;

public partial class SetpointEditor : UserControl
{
	public static readonly DependencyProperty LabelProperty=
		DependencyProperty.Register(
			nameof(Label),
			typeof(string),
			typeof(SetpointEditor));
	public static readonly DependencyProperty TextProperty=
		DependencyProperty.Register(
			nameof(Text),
			typeof(string),
			typeof(SetpointEditor),
			new FrameworkPropertyMetadata(
				string.Empty,
				FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
	public static readonly DependencyProperty UnitProperty=
		DependencyProperty.Register(
			nameof(Unit),
			typeof(string),
			typeof(SetpointEditor));
	public static readonly DependencyProperty IncrementCommandProperty=
		DependencyProperty.Register(
			nameof(IncrementCommand),
			typeof(ICommand),
			typeof(SetpointEditor));
	public static readonly DependencyProperty DecrementCommandProperty=
		DependencyProperty.Register(
			nameof(DecrementCommand),
			typeof(ICommand),
			typeof(SetpointEditor));
	public static readonly DependencyProperty CommitCommandProperty=
		DependencyProperty.Register(
			nameof(CommitCommand),
			typeof(ICommand),
			typeof(SetpointEditor));

	public SetpointEditor()
	{
		InitializeComponent();
	}

	private void TextBoxPreviewKeyDown(object sender,KeyEventArgs eventArgs)
	{
		if(eventArgs.Key != Key.Enter)
		{
			return;
		}
		if(CommitCommand?.CanExecute(null) == true)
		{
			CommitCommand.Execute(null);
		}
		eventArgs.Handled=true;
		Root.Focus();
	}

	public string Label
	{
		get => (string)GetValue(LabelProperty);
		set => SetValue(LabelProperty,value);
	}

	public string Text
	{
		get => (string)GetValue(TextProperty);
		set => SetValue(TextProperty,value);
	}

	public string Unit
	{
		get => (string)GetValue(UnitProperty);
		set => SetValue(UnitProperty,value);
	}

	public ICommand IncrementCommand
	{
		get => (ICommand)GetValue(IncrementCommandProperty);
		set => SetValue(IncrementCommandProperty,value);
	}

	public ICommand DecrementCommand
	{
		get => (ICommand)GetValue(DecrementCommandProperty);
		set => SetValue(DecrementCommandProperty,value);
	}

	public ICommand CommitCommand
	{
		get => (ICommand)GetValue(CommitCommandProperty);
		set => SetValue(CommitCommandProperty,value);
	}
}
