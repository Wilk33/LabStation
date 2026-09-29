using System.Diagnostics.CodeAnalysis;
using Ka3005P.Core.Protocol;

namespace Ka3005P.Core.Sessions;

public abstract record SessionRequest;

public sealed record SetVoltageRequest(VoltageSetpoint Value) : SessionRequest;

public sealed record SetCurrentRequest(CurrentSetpoint Value) : SessionRequest;

public sealed record SetOutputRequest : SessionRequest
{
	internal SetOutputRequest(bool enabled)
	{
		Enabled=enabled;
	}

	public bool Enabled { get; }
	internal TaskCompletionSource Completion { get; }=
		new(TaskCreationOptions.RunContinuationsAsynchronously);
}

public sealed record MeasurementRequest : SessionRequest
{
	internal TaskCompletionSource Completion { get; }=
		new(TaskCreationOptions.RunContinuationsAsynchronously);
}

public sealed record CloseSessionRequest : SessionRequest;

public sealed class SessionRequestQueue
{
	private readonly object gate=new();
	private readonly SemaphoreSlim signal=new(0,1);
	private SetVoltageRequest? pendingVoltage;
	private SetCurrentRequest? pendingCurrent;
	private SetOutputRequest? pendingOutput;
	private MeasurementRequest? pendingMeasurement;
	private CloseSessionRequest? pendingClose;

	public int PendingRequestCount
	{
		get
		{
			lock(gate)
			{
				return CountPending();
			}
		}
	}

	public int PendingSetpointCount
	{
		get
		{
			lock(gate)
			{
				return (pendingVoltage is null ? 0 : 1)+
					(pendingCurrent is null ? 0 : 1);
			}
		}
	}

	public void SetVoltage(VoltageSetpoint value)
	{
		lock(gate)
		{
			if(pendingClose is not null)
			{
				return;
			}

			pendingVoltage=new SetVoltageRequest(value);
		}
		Signal();
	}

	public void SetCurrent(CurrentSetpoint value)
	{
		lock(gate)
		{
			if(pendingClose is not null)
			{
				return;
			}

			pendingCurrent=new SetCurrentRequest(value);
		}
		Signal();
	}

	public SetOutputRequest SetOutput(bool enabled)
	{
		SetOutputRequest request;
		lock(gate)
		{
			if(pendingClose is not null)
			{
				throw new InvalidOperationException("Sesja jest zamykana.");
			}

			if(pendingOutput is not null && pendingOutput.Enabled == enabled)
			{
				return pendingOutput;
			}

			pendingOutput?.Completion.TrySetCanceled();
			request=new SetOutputRequest(enabled);
			pendingOutput=request;
			if(!enabled)
			{
				pendingVoltage=null;
				pendingCurrent=null;
				pendingMeasurement?.Completion.TrySetResult();
				pendingMeasurement=null;
			}
		}
		Signal();
		return request;
	}

	public MeasurementRequest RequestMeasurement()
	{
		MeasurementRequest request;
		lock(gate)
		{
			if(pendingClose is not null)
			{
				throw new InvalidOperationException("Sesja jest zamykana.");
			}

			pendingMeasurement??=new MeasurementRequest();
			request=pendingMeasurement;
		}
		Signal();
		return request;
	}

	public void Close()
	{
		lock(gate)
		{
			pendingOutput?.Completion.TrySetCanceled();
			pendingVoltage=null;
			pendingCurrent=null;
			pendingOutput=null;
			pendingMeasurement?.Completion.TrySetResult();
			pendingMeasurement=null;
			pendingClose??=new CloseSessionRequest();
		}
		Signal();
	}

	public ValueTask WaitAsync(CancellationToken cancellationToken)
	{
		return new ValueTask(signal.WaitAsync(cancellationToken));
	}

	public SessionRequest TakeNext()
	{
		if(!TryTakeNext(out SessionRequest? request))
		{
			throw new InvalidOperationException("Kolejka jest pusta.");
		}

		return request;
	}

	public bool TryTakeNext([NotNullWhen(true)] out SessionRequest? request)
	{
		lock(gate)
		{
			request=TakeClose() ??
				TakeOutput() ??
				TakeVoltage() ??
				TakeCurrent() ??
				TakeMeasurement();
			return request is not null;
		}
	}

	private SessionRequest? TakeClose()
	{
		CloseSessionRequest? request=pendingClose;
		pendingClose=null;
		return request;
	}

	private SessionRequest? TakeOutput()
	{
		SetOutputRequest? request=pendingOutput;
		pendingOutput=null;
		return request;
	}

	private SessionRequest? TakeVoltage()
	{
		SetVoltageRequest? request=pendingVoltage;
		pendingVoltage=null;
		return request;
	}

	private SessionRequest? TakeCurrent()
	{
		SetCurrentRequest? request=pendingCurrent;
		pendingCurrent=null;
		return request;
	}

	private SessionRequest? TakeMeasurement()
	{
		MeasurementRequest? request=pendingMeasurement;
		pendingMeasurement=null;
		return request;
	}

	private int CountPending()
	{
		return (pendingVoltage is null ? 0 : 1)+
			(pendingCurrent is null ? 0 : 1)+
			(pendingOutput is null ? 0 : 1)+
			(pendingMeasurement is null ? 0 : 1)+
			(pendingClose is null ? 0 : 1);
	}

	private void Signal()
	{
		try
		{
			signal.Release();
		}
		catch(SemaphoreFullException)
		{
		}
	}
}
