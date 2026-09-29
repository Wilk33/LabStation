using Ka3005P.App.ViewModels;
using Ka3005P.Core.Measurements;
using Ka3005P.Tests.Fakes;
using Ka3005P.App.Views;
using LabStation.UI.Controls;
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
			ChartWindow window=new();
			Assert.Equal(360,window.Height);
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
	public void Measurement_UpdatesTotalValuesResistanceAndBothSeries()
	{
		FakeOutputController output=new();
		FakeChartSampleSource source=new();
		using ChartViewModel chart=new(output,source,null,null);

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
