using System.Diagnostics;
using Ka3005P.Core.Device;
using Ka3005P.Core.Dual;
using Ka3005P.Core.Protocol;
using Ka3005P.Core.Sessions;
using Ka3005P.Core.Transport;

namespace Ka3005P.HardwareSmoke;

internal static class Program
{
	private static async Task<int> Main(string[] args)
	{
		if(args.Length == 3 &&
			string.Equals(args[0],"--probe",StringComparison.OrdinalIgnoreCase))
		{
			return await RawSerialProbe.RunAsync(args[1],args[2]);
		}
		if(args.Length != 2)
		{
			Console.Error.WriteLine(
				"Użycie: Ka3005P.HardwareSmoke COMx COMy lub --probe COMx COMy");
			return 2;
		}

		using CancellationTokenSource timeout=new(TimeSpan.FromSeconds(30));
		IPowerSupplySession? first=null;
		IPowerSupplySession? second=null;
		DualPowerSupplyController? controller=null;
		try
		{
			Console.WriteLine($"Otwieranie {args[0]} i {args[1]}...");
			first=await CreateSessionAsync(args[0],timeout.Token);
			second=await CreateSessionAsync(args[1],timeout.Token);
			controller=new DualPowerSupplyController(
				first,
				second,
				DualMode.Parallel);

			controller.RequestVoltage(VoltageSetpoint.FromHundredths(100));
			controller.RequestCurrent(CurrentSetpoint.FromThousandths(200));
			await WaitForSetpointsAsync(first,second,100,100,timeout.Token);
			Console.WriteLine("Nastawy początkowe wysłane: 1,00 V i 0,100 A na zasilacz.");

			DualOperationResult on=await controller.SetOutputAsync(true,timeout.Token);
			EnsureSuccess(on,"ON");
			Console.WriteLine("Oba wyjścia ON.");

			for(int step=0;step<=50;step++)
			{
				controller.RequestVoltage(
					VoltageSetpoint.FromHundredths(100+step));
				controller.RequestCurrent(
					CurrentSetpoint.FromThousandths(200+step*4));
			}
			await WaitForSetpointsAsync(first,second,150,200,timeout.Token);
			Console.WriteLine("Szybka seria zakończona: 1,50 V i 0,200 A na zasilacz.");
			await MeasureCadenceAsync(first,second,timeout.Token);

			await Task.Delay(500,timeout.Token);
			long measurementStartedAt=Stopwatch.GetTimestamp();
			first.RequestMeasurement();
			second.RequestMeasurement();
			await WaitUntilAsync(
				()=>controller.Snapshot.LastMeasurement is DualMeasurement measurement &&
					measurement.First.Timestamp >= measurementStartedAt &&
					measurement.Second.Timestamp >= measurementStartedAt,
				first,
				second,
				timeout.Token);
			DualMeasurement measurement=controller.Snapshot.LastMeasurement!.Value;
			Console.WriteLine(
				$"Pomiar: {measurement.VoltageHundredths/100m:0.00} V, "+
				$"{measurement.CurrentThousandths/1000m:0.000} A.");

			DualOperationResult off=await controller.SetOutputAsync(false,timeout.Token);
			EnsureSuccess(off,"OFF");
			Console.WriteLine("Oba wyjścia OFF. Test zakończony.");
			return 0;
		}
		finally
		{
			if(controller is not null)
			{
				try
				{
					await controller.SetOutputAsync(false,CancellationToken.None);
				}
				catch(Exception exception)
				{
					Console.Error.WriteLine("Awaryjne OFF: "+exception.Message);
				}
				await controller.DisposeAsync();
			}
			else
			{
				await StopSessionAsync(first);
				await StopSessionAsync(second);
			}
		}
	}

	private static async ValueTask<IPowerSupplySession> CreateSessionAsync(
		string portName,
		CancellationToken cancellationToken)
	{
		SerialPortTransport transport=new(
			TimeSpan.FromMilliseconds(250),
			TimeSpan.FromMilliseconds(250));
		try
		{
			await transport.OpenAsync(portName,cancellationToken);
			Ka3005PDevice device=new(transport,TimeSpan.FromMilliseconds(350));
			PowerSupplySession session=new(
				device,
				TimeProvider.System,
				TimeSpan.FromMilliseconds(100));
			await session.StartAsync(cancellationToken);
			return session;
		}
		catch
		{
			await transport.DisposeAsync();
			throw;
		}
	}

	private static async Task MeasureCadenceAsync(
		IPowerSupplySession first,
		IPowerSupplySession second,
		CancellationToken cancellationToken)
	{
		object gate=new();
		List<long> firstTimestamps=[];
		List<long> secondTimestamps=[];
		void RecordFirst(object? sender,Ka3005P.Core.Measurements.MeasurementSample sample)
		{
			lock(gate)
			{
				firstTimestamps.Add(sample.Timestamp);
			}
		}
		void RecordSecond(object? sender,Ka3005P.Core.Measurements.MeasurementSample sample)
		{
			lock(gate)
			{
				secondTimestamps.Add(sample.Timestamp);
			}
		}

		first.MeasurementReceived+=RecordFirst;
		second.MeasurementReceived+=RecordSecond;
		try
		{
			await WaitUntilAsync(
				()=>
				{
					lock(gate)
					{
						return firstTimestamps.Count>=10 &&
							secondTimestamps.Count>=10;
					}
				},
				first,
				second,
				cancellationToken);
		}
		finally
		{
			first.MeasurementReceived-=RecordFirst;
			second.MeasurementReceived-=RecordSecond;
		}

		long[] firstSnapshot;
		long[] secondSnapshot;
		lock(gate)
		{
			firstSnapshot=[.. firstTimestamps];
			secondSnapshot=[.. secondTimestamps];
		}
		Console.WriteLine("Odstęp próbek 1: "+FormatCadence(firstSnapshot));
		Console.WriteLine("Odstęp próbek 2: "+FormatCadence(secondSnapshot));
	}

	private static string FormatCadence(IReadOnlyList<long> timestamps)
	{
		double[] intervals=Enumerable.Range(1,timestamps.Count-1)
			.Select(index=>Stopwatch.GetElapsedTime(
				timestamps[index-1],
				timestamps[index]).TotalMilliseconds)
			.ToArray();
		return $"średnio {intervals.Average():0.0} ms, "+
			$"min {intervals.Min():0.0} ms, max {intervals.Max():0.0} ms, "+
			$"próbek {timestamps.Count}";
	}

	private static async Task WaitForSetpointsAsync(
		IPowerSupplySession first,
		IPowerSupplySession second,
		int voltageHundredths,
		int currentThousandths,
		CancellationToken cancellationToken)
	{
		await WaitUntilAsync(
			()=>first.Snapshot.SentVoltage?.Hundredths == voltageHundredths &&
				second.Snapshot.SentVoltage?.Hundredths == voltageHundredths &&
				first.Snapshot.SentCurrent?.Thousandths == currentThousandths &&
				second.Snapshot.SentCurrent?.Thousandths == currentThousandths,
			first,
			second,
			cancellationToken);
	}

	private static async Task WaitUntilAsync(
		Func<bool> condition,
		IPowerSupplySession first,
		IPowerSupplySession second,
		CancellationToken cancellationToken)
	{
		while(!condition())
		{
			ThrowIfSessionFailed(first,argsName:"pierwszy");
			ThrowIfSessionFailed(second,argsName:"drugi");
			await Task.Delay(20,cancellationToken);
		}
	}

	private static void ThrowIfSessionFailed(
		IPowerSupplySession session,
		string argsName)
	{
		if(session.Snapshot.Error is SessionError error)
		{
			throw new IOException($"Błąd sesji {argsName}: {error.Message}",error.Exception);
		}
	}

	private static void EnsureSuccess(DualOperationResult result,string operation)
	{
		if(!result.IsSuccess)
		{
			throw new IOException(
				$"{operation} nie powiodło się: "+
				$"pierwszy={result.FirstError?.Message ?? "OK"}; "+
				$"drugi={result.SecondError?.Message ?? "OK"}.");
		}
	}

	private static async Task StopSessionAsync(IPowerSupplySession? session)
	{
		if(session is null)
		{
			return;
		}
		try
		{
			await session.SetOutputAsync(false,CancellationToken.None);
		}
		catch(Exception exception)
		{
			Console.Error.WriteLine("Awaryjne OFF: "+exception.Message);
		}
		await session.DisposeAsync();
	}
}
