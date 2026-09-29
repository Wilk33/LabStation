using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Sdg1032X.Core;

namespace Sdg1032X.App;

public partial class GeneratorView : UserControl
{
	private GeneratorSession? session;
	private bool connecting;

	public GeneratorView()
	{
		InitializeComponent();
		Channel1.Configure(1,()=>session,ShowStatus);
		Channel2.Configure(2,()=>session,ShowStatus);
		Channel1.SummaryChanged+=(_, eventArgs)=>Channel1Header.Text=eventArgs.Text;
		Channel2.SummaryChanged+=(_, eventArgs)=>Channel2Header.Text=eventArgs.Text;
		SetControlsEnabled(false);
		Unloaded+=GeneratorViewUnloaded;
	}


	private async void ConnectionClick(object sender,RoutedEventArgs eventArgs)
	{
		if(session is null)
		{
			await ConnectAsync();
		}
		else
		{
			await DisconnectAsync();
		}
	}

	private async Task ConnectAsync()
	{
		if(connecting)
		{
			return;
		}
		string host=HostEditor.Text.Trim();
		if(string.IsNullOrWhiteSpace(host))
		{
			ShowStatus("Wprowadź adres IP generatora.",true);
			return;
		}
		connecting=true;
		ConnectionButton.IsEnabled=false;
		ConnectionButton.Content="Łączenie";
		ShowStatus("Łączenie z "+host+"...",false);
		try
		{
			session=await GeneratorSession.ConnectAsync(host);
			session.CommunicationFailed+=SessionCommunicationFailed;
			ChannelSnapshot[] snapshots=await Task.WhenAll(
				session.ReadChannelAsync(1),
				session.ReadChannelAsync(2));
			Channel1.ApplySnapshot(snapshots[0]);
			Channel2.ApplySnapshot(snapshots[1]);
			SetControlsEnabled(true);
			HostEditor.IsEnabled=false;
			ConnectionButton.Content="Online";
			ConnectionButton.Background=new SolidColorBrush(Color.FromRgb(22,135,70));
			ConnectionButton.Foreground=Brushes.White;
			ShowStatus("Połączono: "+session.Identity,false);
		}
		catch(Exception exception)
		{
			if(session is not null)
			{
				await DisposeSessionAsync(session);
				session=null;
			}
			ShowStatus("Błąd połączenia: "+exception.Message,true);
			ConnectionButton.Content="Offline";
		}
		finally
		{
			connecting=false;
			ConnectionButton.IsEnabled=true;
		}
	}

	private async Task DisconnectAsync()
	{
		GeneratorSession? current=session;
		session=null;
		SetControlsEnabled(false);
		HostEditor.IsEnabled=true;
		ConnectionButton.IsEnabled=false;
		if(current is not null)
		{
			await DisposeSessionAsync(current);
		}
		ConnectionButton.Content="Offline";
		ConnectionButton.Background=(Brush)FindResource("LabStationInputBrush");
		ConnectionButton.Foreground=Brushes.Black;
		ConnectionButton.IsEnabled=true;
		ShowStatus("Stan generatora: OFFLINE",false);
	}

	private async void GeneratorViewUnloaded(object? sender,EventArgs eventArgs)
	{
		if(session is not null)
		{
			GeneratorSession current=session;
			session=null;
			await DisposeSessionAsync(current);
		}
	}

	private void SessionCommunicationFailed(object? sender,Exception exception)
	{
		if(Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
		{
			return;
		}
		if(Dispatcher.CheckAccess())
		{
			ShowStatus("Błąd komunikacji: "+exception.Message,true);
			return;
		}
		Dispatcher.InvokeAsync(()=>
			ShowStatus("Błąd komunikacji: "+exception.Message,true));
	}

	private async Task DisposeSessionAsync(GeneratorSession current)
	{
		current.CommunicationFailed-=SessionCommunicationFailed;
		await current.DisposeAsync();
	}

	private void SetControlsEnabled(bool enabled)
	{
		Channel1.IsEnabled=enabled;
		Channel2.IsEnabled=enabled;
	}

	private void ShowStatus(string message,bool error)
	{
		StatusText.Text=message;
		StatusText.Foreground=error
			? new SolidColorBrush(Color.FromRgb(255,128,128))
			: (Brush)FindResource("MutedTextBrush");
	}

}
