using Ka3005P.Core.Measurements;

namespace Ka3005P.Tests.Measurements;

public sealed class MeasurementRecorderTests
{
	[Fact]
	public async Task TryRecord_WhenBufferIsFull_StopsRecorderAndReportsOneFailure()
	{
		BlockingMeasurementSink sink=new();
		await using MeasurementRecorder recorder=new(sink,capacity:2);

		Assert.True(recorder.TryRecord(Sample(1)));
		Assert.True(recorder.TryRecord(Sample(2)));
		Assert.False(recorder.TryRecord(Sample(3)));
		Assert.False(recorder.TryRecord(Sample(4)));

		Assert.Equal(RecordingState.Faulted,recorder.State);
		Assert.Single(recorder.Failures);
		sink.Release();
		await recorder.CompleteAsync(CancellationToken.None);
	}

	[Fact]
	public async Task CompleteAsync_DrainsAcceptedSamplesInOrder()
	{
		CollectingMeasurementSink sink=new();
		await using MeasurementRecorder recorder=new(sink,capacity:4);
		recorder.TryRecord(Sample(1));
		recorder.TryRecord(Sample(2));

		await recorder.CompleteAsync(CancellationToken.None);

		Assert.Equal([1L,2L],sink.Samples.Select(sample=>sample.Timestamp));
		Assert.Equal(RecordingState.Completed,recorder.State);
	}

	private static MeasurementSample Sample(long timestamp)
	{
		return new MeasurementSample(
			DateTimeOffset.UnixEpoch,
			timestamp,
			1200,
			100);
	}

	private sealed class BlockingMeasurementSink : IMeasurementSink
	{
		private readonly TaskCompletionSource release=
			new(TaskCreationOptions.RunContinuationsAsynchronously);

		public async ValueTask WriteAsync(
			MeasurementSample sample,
			CancellationToken cancellationToken)
		{
			await release.Task.WaitAsync(cancellationToken);
		}

		public ValueTask CompleteAsync(CancellationToken cancellationToken)
		{
			return ValueTask.CompletedTask;
		}

		public void Release()
		{
			release.TrySetResult();
		}
	}

	private sealed class CollectingMeasurementSink : IMeasurementSink
	{
		public List<MeasurementSample> Samples { get; }=[];

		public ValueTask WriteAsync(
			MeasurementSample sample,
			CancellationToken cancellationToken)
		{
			Samples.Add(sample);
			return ValueTask.CompletedTask;
		}

		public ValueTask CompleteAsync(CancellationToken cancellationToken)
		{
			return ValueTask.CompletedTask;
		}
	}
}
