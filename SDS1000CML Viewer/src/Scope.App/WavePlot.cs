using System.Windows.Media;
using LabStation.UI.Controls;
using Scope.Core;

namespace Scope.App;

public sealed class WavePlot : TimeSeriesPlot
{
	private HashSet<int> visibleChannels=[1,2];
	private Waveform[] waves=[];

	public WavePlot()
	{
		CursorCount=4;
		CursorsEnabled=true;
		HorizontalTitle="Czas";
		HorizontalUnit="s";
		EmptyMessage="Połącz oscyloskop, aby wyświetlić przebieg";
	}

	public bool HasWaveforms=>
		waves.Any(wave=>visibleChannels.Contains(wave.Channel));

	public int[] VisibleWaveChannels=>waves
		.Where(wave=>visibleChannels.Contains(wave.Channel))
		.Select(wave=>wave.Channel)
		.Distinct()
		.OrderBy(channel=>channel)
		.ToArray();

	public (double Min,double Max) VisibleTimeRange=>VisibleRange;

	public void SetWaveforms(Waveform[] value)
	{
		ArgumentNullException.ThrowIfNull(value);
		waves=value;
		Stale=false;
		RefreshSeries();
	}

	public void SetVisibleChannels(int[] channels)
	{
		ArgumentNullException.ThrowIfNull(channels);
		if(channels.Any(channel=>channel is not (1 or 2)))
		{
			throw new ArgumentException("Wybierz CH1 lub CH2.",nameof(channels));
		}
		visibleChannels=channels.Distinct().ToHashSet();
		if(visibleChannels.Count == 0)
		{
			ClearCursors();
		}
		RefreshSeries();
	}

	public double? CursorTime(int index)=>CursorPosition(index);

	private void RefreshSeries()
	{
		SetSeries(waves
			.Where(wave=>visibleChannels.Contains(wave.Channel))
			.OrderBy(wave=>wave.Channel)
			.Select(wave=>new PlotSeries(
				"CH"+wave.Channel,
				"V",
				wave.Channel == 1
					? Color.FromRgb(255,220,50)
					: Color.FromRgb(80,225,230),
				PlotAxis.Left,
				Enumerable.Range(0,wave.Volts.Length)
					.Select(index=>new PlotPoint(
						wave.Start+index*wave.Interval,
						wave.Volts[index]))
					.ToArray())));
	}
}
