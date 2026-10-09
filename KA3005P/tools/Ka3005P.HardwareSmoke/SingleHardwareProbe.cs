using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using Ka3005P.Core.Device;
using Ka3005P.Core.Protocol;
using Ka3005P.Core.Sessions;
using Ka3005P.Core.Transport;

namespace Ka3005P.HardwareSmoke;

internal static class SingleHardwareProbe
{
	private const int MaximumVoltageHundredths=1250;
	private const int MaximumCurrentThousandths=1500;
	private static readonly Encoding Ascii=Encoding.ASCII;

	public static async Task<int> InspectAsync(string portName)
	{
		using SerialPort port=CreatePort(portName);
		port.Open();
		Console.WriteLine($"{DateTimeOffset.Now:O} Otwarto {portName}. Odczyt bez zmiany nastaw.");
		for(int sample=1;sample<=10;sample++)
		{
			string voltage=Query(port,"VOUT1?",5);
			string current=Query(port,"IOUT1?",5);
			Console.WriteLine(
				$"{DateTimeOffset.Now:O} próbka={sample} U='{voltage}' I='{current}'");
			await Task.Delay(200);
		}

		return 0;
	}

	public static async Task<int> StressAsync(
		string portName,
		string scenario,
		TimeSpan duration)
	{
		using CancellationTokenSource timeout=new(duration+TimeSpan.FromSeconds(15));
		SerialPortTransport? transport=null;
		PowerSupplySession? session=null;
		try
		{
			transport=new SerialPortTransport(
				TimeSpan.FromMilliseconds(500),
				TimeSpan.FromMilliseconds(500));
			await transport.OpenAsync(portName,timeout.Token);
			Ka3005PDevice device=new(transport,TimeSpan.FromMilliseconds(1500));
			transport=null;
			session=new PowerSupplySession(
				device,
				TimeProvider.System,
				TimeSpan.FromMilliseconds(100));
			await session.StartAsync(timeout.Token);
			Console.WriteLine(
				$"{DateTimeOffset.Now:O} START port={portName} scenariusz={scenario} "+
				$"czas={duration.TotalSeconds:0}s");

			SetSafeValues(session,500,500);
			await WaitForSetpointsAsync(session,500,500,timeout.Token);
			await session.SetOutputAsync(false,timeout.Token);
			if(!scenario.Equals("off",StringComparison.OrdinalIgnoreCase))
			{
				await session.SetOutputAsync(true,timeout.Token);
			}

			long started=Stopwatch.GetTimestamp();
			long nextReport=started;
			int step=0;
			while(Stopwatch.GetElapsedTime(started)<duration)
			{
				ApplyScenario(session,scenario,step++);
				SessionSnapshot snapshot=session.Snapshot;
				if(snapshot.Error is SessionError error)
				{
					throw new IOException(
						$"Błąd sesji: {error.Message}",
						error.Exception);
				}

				if(Stopwatch.GetElapsedTime(nextReport)>=TimeSpan.FromSeconds(10))
				{
					nextReport=Stopwatch.GetTimestamp();
					Console.WriteLine(FormatSnapshot(session.Snapshot,started,step));
				}

				await Task.Delay(100,timeout.Token);
			}

			await session.SetOutputAsync(false,timeout.Token);
			Console.WriteLine(
				$"{DateTimeOffset.Now:O} PASS scenariusz={scenario} "+
				$"kroki={step} {FormatMeasurement(session.Snapshot)}");
			return 0;
		}
		catch(Exception exception)
		{
			Console.Error.WriteLine(
				$"{DateTimeOffset.Now:O} FAIL scenariusz={scenario}: {exception}");
			return 1;
		}
		finally
		{
			if(session is not null)
			{
				try
				{
					using CancellationTokenSource offTimeout=new(TimeSpan.FromSeconds(2));
					await session.SetOutputAsync(false,offTimeout.Token);
					Console.WriteLine($"{DateTimeOffset.Now:O} Potwierdzono polecenie OUT0.");
				}
				catch(Exception exception)
				{
					Console.Error.WriteLine(
						$"{DateTimeOffset.Now:O} Awaryjne OUT0 przez sesję: {exception.Message}");
				}

				try
				{
					await session.DisposeAsync();
				}
				catch(Exception exception)
				{
					Console.Error.WriteLine(
						$"{DateTimeOffset.Now:O} Zamykanie sesji: {exception.Message}");
				}
			}

			if(transport is not null)
			{
				await transport.DisposeAsync();
			}

			EmergencyOff(portName);
		}
	}

	private static void ApplyScenario(
		PowerSupplySession session,
		string scenario,
		int step)
	{
		switch(scenario.ToLowerInvariant())
		{
			case "off":
			case "steady":
				return;
			case "voltage":
				session.RequestVoltage(VoltageSetpoint.FromHundredths(500+step%76*10));
				return;
			case "current":
				session.RequestCurrent(CurrentSetpoint.FromThousandths(300+step%61*20));
				return;
			case "combined":
				session.RequestVoltage(VoltageSetpoint.FromHundredths(500+step%76*10));
				session.RequestCurrent(CurrentSetpoint.FromThousandths(300+step%61*20));
				return;
			default:
				throw new ArgumentException(
					"Scenariusz musi mieć wartość: off, steady, voltage, current lub combined.",
					nameof(scenario));
		}
	}

	private static void SetSafeValues(
		PowerSupplySession session,
		int voltageHundredths,
		int currentThousandths)
	{
		if(voltageHundredths>MaximumVoltageHundredths ||
			currentThousandths>MaximumCurrentThousandths)
		{
			throw new InvalidOperationException("Test przekracza bezpieczne ograniczenia.");
		}

		session.RequestVoltage(VoltageSetpoint.FromHundredths(voltageHundredths));
		session.RequestCurrent(CurrentSetpoint.FromThousandths(currentThousandths));
	}

	private static async Task WaitForSetpointsAsync(
		PowerSupplySession session,
		int voltageHundredths,
		int currentThousandths,
		CancellationToken cancellationToken)
	{
		while(session.Snapshot.SentVoltage?.Hundredths != voltageHundredths ||
			session.Snapshot.SentCurrent?.Thousandths != currentThousandths)
		{
			if(session.Snapshot.Error is SessionError error)
			{
				throw new IOException(error.Message,error.Exception);
			}

			await Task.Delay(10,cancellationToken);
		}
	}

	private static string FormatSnapshot(
		SessionSnapshot snapshot,
		long started,
		int step)
	{
		return $"{DateTimeOffset.Now:O} t={Stopwatch.GetElapsedTime(started).TotalSeconds:0.0}s "+
			$"krok={step} oczekuje={snapshot.RequestedVoltage?.Hundredths}/"+
			$"{snapshot.RequestedCurrent?.Thousandths} wysłano="+
			$"{snapshot.SentVoltage?.Hundredths}/{snapshot.SentCurrent?.Thousandths} "+
			FormatMeasurement(snapshot);
	}

	private static string FormatMeasurement(SessionSnapshot snapshot)
	{
		DeviceMeasurement? measurement=snapshot.LastMeasurement;
		return measurement is null
			? "pomiar=brak"
			: $"pomiar={measurement.Value.VoltageHundredths/100m:0.00}V/"+
				$"{measurement.Value.CurrentThousandths/1000m:0.000}A";
	}

	private static SerialPort CreatePort(string portName)
	{
		return new SerialPort(portName,9600,Parity.None,8,StopBits.One)
		{
			Handshake=Handshake.None,
			DtrEnable=false,
			RtsEnable=false,
			ReadTimeout=1000,
			WriteTimeout=1000
		};
	}

	private static string Query(SerialPort port,string command,int replyLength)
	{
		port.DiscardInBuffer();
		byte[] request=Ascii.GetBytes(command);
		byte[] response=new byte[replyLength];
		port.Write(request,0,request.Length);
		int offset=0;
		while(offset<response.Length)
		{
			offset+=port.Read(response,offset,response.Length-offset);
		}

		Thread.Sleep(45);
		return Ascii.GetString(response);
	}

	private static void EmergencyOff(string portName)
	{
		try
		{
			using SerialPort port=CreatePort(portName);
			port.Open();
			byte[] command=Ascii.GetBytes("OUT0");
			port.Write(command,0,command.Length);
			Thread.Sleep(45);
			Console.WriteLine($"{DateTimeOffset.Now:O} Awaryjne OUT0 wysłane osobnym połączeniem.");
		}
		catch(Exception exception)
		{
			Console.Error.WriteLine(
				$"{DateTimeOffset.Now:O} Awaryjne OUT0 nie powiodło się: {exception.Message}");
		}
	}
}
