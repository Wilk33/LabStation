using Ka3005P.Core.Device;
using Ka3005P.Core.Protocol;

namespace Ka3005P.Tests.Fakes;

internal sealed class FakePowerSupplyDevice : IPowerSupplyDevice
{
	private readonly object gate=new();
	private readonly List<int> voltageWrites=[];
	private readonly List<int> currentWrites=[];
	private readonly List<bool> outputWrites=[];
	private readonly List<string> operations=[];
	private TaskCompletionSource operationStarted=NewSignal();
	private TaskCompletionSource operationRelease=NewSignal();
	private bool blockNextOperation;
	private bool blockNextMeasurement;
	private TaskCompletionSource measurementStarted=NewSignal();
	private int measurementReads;
	private int activeOperations;
	private int maximumConcurrentOperations;

	public IReadOnlyList<int> VoltageWrites
	{
		get
		{
			lock(gate)
			{
				return voltageWrites.ToArray();
			}
		}
	}

	public IReadOnlyList<int> CurrentWrites
	{
		get
		{
			lock(gate)
			{
				return currentWrites.ToArray();
			}
		}
	}

	public IReadOnlyList<bool> OutputWrites
	{
		get
		{
			lock(gate)
			{
				return outputWrites.ToArray();
			}
		}
	}

	public IReadOnlyList<string> Operations
	{
		get
		{
			lock(gate)
			{
				return operations.ToArray();
			}
		}
	}

	public int MaximumConcurrentOperations => Volatile.Read(ref maximumConcurrentOperations);
	public int MeasurementReads => Volatile.Read(ref measurementReads);
	public DeviceMeasurement Measurement { get; set; }=new(1200,100);

	public void BlockNextOperation()
	{
		lock(gate)
		{
			blockNextOperation=true;
			operationStarted=NewSignal();
			operationRelease=NewSignal();
		}
	}

	public Task WaitUntilOperationStartsAsync()
	{
		lock(gate)
		{
			return operationStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
		}
	}

	public void BlockNextMeasurement()
	{
		lock(gate)
		{
			blockNextMeasurement=true;
			measurementStarted=NewSignal();
			operationRelease=NewSignal();
		}
	}

	public Task WaitUntilMeasurementStartsAsync()
	{
		lock(gate)
		{
			return measurementStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
		}
	}

	public Task WaitForMeasurementReadsAsync(int count)
	{
		return WaitUntilAsync(()=>MeasurementReads >= count);
	}

	public void ReleaseOperation()
	{
		lock(gate)
		{
			operationRelease.TrySetResult();
		}
	}

	public Task WaitForVoltageAsync(int hundredths)
	{
		return WaitUntilAsync(()=>VoltageWrites.Contains(hundredths));
	}

	public Task WaitForOutputAsync(bool enabled)
	{
		return WaitUntilAsync(()=>OutputWrites.Contains(enabled));
	}

	public ValueTask SetVoltageAsync(
		VoltageSetpoint value,
		CancellationToken cancellationToken)
	{
		return ExecuteAsync(
			$"Voltage:{value.Hundredths}",
			()=>voltageWrites.Add(value.Hundredths),
			cancellationToken);
	}

	public ValueTask SetCurrentAsync(
		CurrentSetpoint value,
		CancellationToken cancellationToken)
	{
		return ExecuteAsync(
			$"Current:{value.Thousandths}",
			()=>currentWrites.Add(value.Thousandths),
			cancellationToken);
	}

	public ValueTask SetOutputAsync(bool enabled,CancellationToken cancellationToken)
	{
		return ExecuteAsync(
			$"Output:{enabled}",
			()=>outputWrites.Add(enabled),
			cancellationToken);
	}

	public async ValueTask<DeviceMeasurement> ReadMeasurementAsync(
		CancellationToken cancellationToken)
	{
		Interlocked.Increment(ref measurementReads);
		Task? release=null;
		lock(gate)
		{
			if(blockNextMeasurement)
			{
				blockNextMeasurement=false;
				measurementStarted.TrySetResult();
				release=operationRelease.Task;
			}
		}
		if(release is not null)
		{
			await release.WaitAsync(cancellationToken);
		}
		await ExecuteAsync("Measurement",()=>{ },cancellationToken);
		return Measurement;
	}

	public ValueTask DisposeAsync()
	{
		return ValueTask.CompletedTask;
	}

	private async ValueTask ExecuteAsync(
		string name,
		Action record,
		CancellationToken cancellationToken)
	{
		int active=Interlocked.Increment(ref activeOperations);
		UpdateMaximum(active);
		Task? release=null;
		try
		{
			lock(gate)
			{
				if(blockNextOperation)
				{
					blockNextOperation=false;
					operationStarted.TrySetResult();
					release=operationRelease.Task;
				}
			}

			if(release is not null)
			{
				await release.WaitAsync(cancellationToken);
			}

			lock(gate)
			{
				operations.Add(name);
				record();
			}
		}
		finally
		{
			Interlocked.Decrement(ref activeOperations);
		}
	}

	private void UpdateMaximum(int active)
	{
		int observed=Volatile.Read(ref maximumConcurrentOperations);
		while(active > observed)
		{
			int exchanged=Interlocked.CompareExchange(
				ref maximumConcurrentOperations,
				active,
				observed);
			if(exchanged == observed)
			{
				return;
			}

			observed=exchanged;
		}
	}

	private static async Task WaitUntilAsync(Func<bool> predicate)
	{
		using CancellationTokenSource timeout=new(TimeSpan.FromSeconds(2));
		while(!predicate())
		{
			await Task.Delay(10,timeout.Token);
		}
	}

	private static TaskCompletionSource NewSignal()
	{
		return new(TaskCreationOptions.RunContinuationsAsynchronously);
	}
}
