using System.Windows;
using LabStation.UI;

namespace Sdg1032X.App;

public partial class MainWindow : Window
{
	
public MainWindow()
	
{
	
	
InitializeComponent();
	
	
Title=ProductInformation.GetWindowTitle();
	
	
SystemTheme.ApplyTo(this);
	
}
}
