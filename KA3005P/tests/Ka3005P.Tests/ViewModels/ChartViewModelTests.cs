using Ka3005P.App.ViewModels;
using Ka3005P.Core.Measurements;
using Ka3005P.Tests.Fakes;
using Ka3005P.App.Views;
using Ka3005P.App.Controls;
using LabStation.UI.Controls;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ka3005P.Tests.ViewModels;

public sealed class ChartViewModelTests
{
	[Fact]
	public void AddSample_KeepsLatest20480AndScalesAxis()
	{
		FakeOutputController output=new();
		using ChartViewModel viewModel=new(output);
		for(int index=0;index<20490;index++)
		{
			viewModel.AddSample(
				TimeSpan.FromMilliseconds(index*100),
				index);
		}

		Assert.Equal(20480,viewModel.Points.Count);
		Assert.Equal(10,viewModel.Points[0].CurrentAmperes);
		Assert.Equal(20489,viewModel.Points[^1].CurrentAmperes);
		Assert.Equal(9.5,viewModel.MinimumY);
		Assert.Equal(20489.5,viewModel.MaximumY);
	}

	[Fact]
	public async Task Cursors_AreAvailableOnlyWhenConnectedOffAndSamplesExist()
	{
		FakeOutputController output=new();
		using ChartViewModel viewModel=new(output);
		output.SetConnected(true);
		viewModel.AddSample(TimeSpan.Zero,1);
		Assert.True(viewModel.CanUseCursors);

		await output.SetOutputAsync(true,CancellationToken.None);
		Assert.False(viewModel.CanUseCursors);
		await output.SetOutputAsync(false,CancellationToken.None);
		Assert.True(viewModel.CanUseCursors);
		output.SetConnected(false);
		Assert.False(viewModel.CanUseCursors);
	}

	[Fact]
	public void SharedPlot_ZoomsAndSupportsTwoCursors()
	{
		RunSta(()=>
		{
			TimeSeriesPlot plot=new()
			{
				CursorCount=2,
				CursorsEnabled=true
			};
			plot.SetSeries(
			[
				new PlotSeries(
					"Prąd",
					"A",
					Colors.Red,
					PlotAxis.Left,
					Enumerable.Range(0,20480)
						.Select(index=>new PlotPoint(index,index/1000d))
						.ToArray())
			]);
			(double fullMin,double fullMax)=plot.VisibleRange;
			plot.ZoomAt(0.5,120);
			(double zoomMin,double zoomMax)=plot.VisibleRange;
			Assert.True(zoomMax-zoomMin<fullMax-fullMin);
			plot.ActivateOrSelectCursor(0);
			plot.ActivateOrSelectCursor(1);
			Assert.Equal([(1,2)],plot.ActiveCursorPairs());
		});
	}

	[Fact]
	public void ChartWindow_DefaultHeightMatchesSingleWindow()
	{
		RunSta(()=>
		{
			Ka3005P.App.App application=new();
			application.InitializeComponent();
			using ChartViewModel viewModel=new(new FakeOutputController());
			ChartWindow window=new();
			window.DataContext=viewModel;
			window.Show();
			window.UpdateLayout();
			Assert.Equal(360,window.Height);
			Button first=(Button)window.FindName("Cursor1Button");
			Button second=(Button)window.FindName("Cursor2Button");
			Button output=(Button)window.FindName("OutputButton");

			Assert.True(Grid.GetColumn(first)<Grid.GetColumn(output));
			Assert.True(Grid.GetColumn(second)<Grid.GetColumn(output));
			Assert.Equal(3,Grid.GetColumn(output));
			window.Close();
			application.Shutdown();
		});
	}

	[Fact]
	public void AddSample_ConstantAndNearZeroValuesKeepVisibleRange()
	{
		using ChartViewModel constant=new(new FakeOutputController());
		constant.AddSample(TimeSpan.Zero,2);
		constant.AddSample(TimeSpan.FromSeconds(1),2);
		Assert.Equal(1.5,constant.MinimumY);
		Assert.Equal(2.5,constant.MaximumY);

		using ChartViewModel nearZero=new(new FakeOutputController());
		nearZero.AddSample(TimeSpan.Zero,0.1);
		nearZero.AddSample(TimeSpan.FromSeconds(1),0.2);
		Assert.Equal(0,nearZero.MinimumY);
		Assert.Equal(0.7,nearZero.MaximumY,6);
	}

	[Fact]
	public async Task ToggleOutput_UsesSameStateForMainAndChart()
	{
		FakePowerSupplySession session=new();
		SingleSupplyViewModel main=new(session);
		using ChartViewModel chart=main.CreateChartViewModel(new FakeFileDialogService());

		await chart.ToggleOutputCommand.ExecuteAsync(null);

		Assert.True(main.IsOutputOn);
		Assert.True(chart.IsOutputOn);
		Assert.Equal([true],session.Outputs);
	}

	[Fact]
	public void ClosingAndReopeningChart_DoesNotRequestMeasurements()
	{
		FakePowerSupplySession session=new();
		SingleSupplyViewModel main=new(session);
		ChartViewModel first=main.CreateChartViewModel(new FakeFileDialogService());
		first.Dispose();
		using ChartViewModel second=main.CreateChartViewModel(new FakeFileDialogService());

		Assert.Equal(0,session.MeasurementRequests);
	}

	[Fact]
	public void ToggleOutput_TracksConnectionState()
	{
		FakeOutputController output=new();
		using ChartViewModel chart=new(output);

		Assert.False(chart.ToggleOutputCommand.CanExecute(null));
		Assert.False(chart.IsOnline);
		Assert.False(chart.IsOff);
		Assert.False(chart.IsOn);
		output.SetConnected(true);
		Assert.True(chart.ToggleOutputCommand.CanExecute(null));
		Assert.True(chart.IsOnline);
		Assert.True(chart.IsOff);
		output.SetConnected(false);
		Assert.False(chart.ToggleOutputCommand.CanExecute(null));
		Assert.False(chart.IsOnline);
		Assert.False(chart.IsOff);
	}

	[Fact]
	public async Task Measurement_UpdatesTotalValuesResistanceAndBothSeries()
	{
		FakeOutputController output=new();
		FakeChartSampleSource source=new();
		using ChartViewModel chart=new(output,source,null,null);
		output.SetConnected(true);
		await output.SetOutputAsync(true,CancellationToken.None);

		source.Publish(new ChartSample(
			TimeSpan.FromSeconds(1),
			10,
			1000,
			true));

		ChartPoint point=Assert.Single(chart.Points);
		Assert.Equal(0.1,point.VoltageVolts);
		Assert.Equal(1,point.CurrentAmperes);
		Assert.Equal("0,10",chart.VoltageText);
		Assert.Equal("1,000",chart.CurrentText);
		Assert.Equal("100 mΩ",chart.ResistanceText);
		Assert.True(chart.IsResistanceVisible);
		Assert.True(chart.ShowCurrent);
		Assert.False(chart.ShowVoltage);
	}

	[Fact]
	public async Task MeasurementTime_CountsOnlyOutputOnAndPreservesPointsAcrossOff()
	{
		FakeOutputController output=new();
		FakeChartSampleSource source=new();
		using ChartViewModel chart=new(output,source,null,null);
		output.SetConnected(true);
		await output.SetOutputAsync(true,CancellationToken.None);
		source.Publish(new ChartSample(TimeSpan.FromSeconds(1),100,100,false));
		source.Publish(new ChartSample(TimeSpan.FromSeconds(2),200,200,false));

		await output.SetOutputAsync(false,CancellationToken.None);
		source.Publish(new ChartSample(TimeSpan.FromSeconds(302),300,300,false));
		await output.SetOutputAsync(true,CancellationToken.None);
		source.Publish(new ChartSample(TimeSpan.FromSeconds(303),400,400,false));
		source.Publish(new ChartSample(TimeSpan.FromSeconds(304),500,500,false));

		Assert.Equal(4,chart.Points.Count);
		Assert.Equal(TimeSpan.Zero,chart.Points[0].Elapsed);
		Assert.Equal(TimeSpan.FromSeconds(1),chart.Points[1].Elapsed);
		Assert.Equal(TimeSpan.FromSeconds(1),chart.Points[2].Elapsed);
		Assert.Equal(TimeSpan.FromSeconds(2),chart.Points[3].Elapsed);
	}

	[Fact]
	public async Task SymmetricMeasurement_ExposesBothPortValuesForHeader()
	{
		FakeOutputController output=new();
		FakeChartSampleSource source=new();
		using ChartViewModel chart=new(output,source,null,null);
		output.SetConnected(true);
		await output.SetOutputAsync(true,CancellationToken.None);

		source.Publish(new ChartSample(
			TimeSpan.FromSeconds(1),
			2400,
			1500,
			false,
			true,
			1200,
			-1200,
			750,
			-750));

		Assert.True(chart.IsSymmetric);
		Assert.Equal("12,00",chart.FirstVoltageText);
		Assert.Equal("-12,00",chart.SecondVoltageText);
		Assert.Equal("0,750",chart.FirstCurrentText);
		Assert.Equal("-0,750",chart.SecondCurrentText);
	}

	[Fact]
	public async Task StatusText_FollowsOfflineOffAndOn()
	{
		FakeOutputController output=new();
		using ChartViewModel chart=new(output);
		Assert.Equal("Stan zasilacza: OFFLINE",chart.StatusText);

		output.SetConnected(true);
		Assert.Equal("Stan zasilacza: OFF",chart.StatusText);

		await output.SetOutputAsync(true,CancellationToken.None);
		Assert.Equal("Stan zasilacza: ON",chart.StatusText);
	}

	[Fact]
	public void CurrentChart_UsesExpandedPlotArea()
	{
		RunSta(()=>
		{
			CurrentChart chart=new()
			{
				Width=600,
				Height=240
			};
			chart.Measure(new System.Windows.Size(600,240));
			chart.Arrange(new System.Windows.Rect(0,0,600,240));

			Assert.True(chart.PlotBounds.Width>444);
			Assert.True(chart.PlotBounds.Height>154);
		});
	}

	private sealed class FakeOutputController : IOutputController
	{
		public event EventHandler<bool>? OutputStateChanged;
		public event EventHandler<bool>? ConnectionStateChanged;
		public bool IsOutputOn { get; private set; }
		public bool IsConnected { get; private set; }

		public void SetConnected(bool connected)
		{
			IsConnected=connected;
			ConnectionStateChanged?.Invoke(this,connected);
		}

		public ValueTask SetOutputAsync(bool enabled,CancellationToken cancellationToken)
		{
			IsOutputOn=enabled;
			OutputStateChanged?.Invoke(this,enabled);
			return ValueTask.CompletedTask;
		}
	}

	private static void RunSta(Action action)
	{
		Exception? failure=null;
		Thread thread=new(()=>
		{
			try
			{
				action();
			}
			catch(Exception exception)
			{
				failure=exception;
			}
		});
		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();
		thread.Join();
		if(failure is not null)
		{
			throw failure;
		}
	}

	private sealed class FakeChartSampleSource : IChartSampleSource
	{
		public event EventHandler<ChartSample>? ChartSampleReceived;

		public void Publish(ChartSample sample)
		{
			ChartSampleReceived?.Invoke(this,sample);
		}
	}
}
