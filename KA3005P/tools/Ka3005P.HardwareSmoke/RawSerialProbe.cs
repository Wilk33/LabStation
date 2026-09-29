using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Text;

namespace Ka3005P.HardwareSmoke;

internal static class RawSerialProbe
{
	private static readonly Encoding Ascii=Encoding.ASCII;

	public static async Task<int> RunAsync(string firstName,string secondName)
	{
		using SerialPort first=CreatePort(firstName);
		using SerialPort second=CreatePort(secondName);
		try
		{
			first.Open();
			second.Open();
			Console.WriteLine($"Otwarto {firstName} i {secondName}.");

			Write(first,"VSET1:01.00");
			Write(second,"VSET1:01.00");
			Write(first,"ISET1:0.100");
			Write(second,"ISET1:0.100");
			await Task.Delay(250);
			Write(first,"OUT1");
			Write(second,"OUT1");
			await Task.Delay(500);

			for(int sample=1;sample<=5;sample++)
			{
				Console.WriteLine($"Próbka {sample}:");
				Query(first,"VOUT1?");
				Query(first,"IOUT1?");
				Query(second,"VOUT1?");
				Query(second,"IOUT1?");
				await Task.Delay(200);
			}

			Stopwatch burst=Stopwatch.StartNew();
			for(int step=0;step<=20;step++)
			{
				string voltage=(100+step).ToString("0000",CultureInfo.InvariantCulture);
				string current=(100+step).ToString("0000",CultureInfo.InvariantCulture);
				string voltageCommand=$"VSET1:{voltage[..2]}.{voltage[2..]}";
				string currentCommand=$"ISET1:{current[..1]}.{current[1..]}";
				Write(first,voltageCommand,false);
				Write(second,voltageCommand,false);
				Write(first,currentCommand,false);
				Write(second,currentCommand,false);
			}
			burst.Stop();
			Console.WriteLine(
				$"Seria 84 zapisów: {burst.Elapsed.TotalMilliseconds:0.000} ms, "+
				$"średnio {burst.Elapsed.TotalMilliseconds/84:0.000} ms/zapis.");
			await Task.Delay(500);
			Console.WriteLine("Pomiar po szybkiej serii:");
			Query(first,"VOUT1?");
			Query(first,"IOUT1?");
			Query(second,"VOUT1?");
			Query(second,"IOUT1?");

			return 0;
		}
		finally
		{
			EmergencyOff(first);
			EmergencyOff(second);
		}
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

	private static double Write(
		SerialPort port,
		string command,
		bool report=true)
	{
		byte[] bytes=Ascii.GetBytes(command);
		Stopwatch stopwatch=Stopwatch.StartNew();
		port.Write(bytes,0,bytes.Length);
		stopwatch.Stop();
		if(report)
		{
			Console.WriteLine(
				$"{port.PortName} TX {command} - {stopwatch.Elapsed.TotalMilliseconds:0.000} ms");
		}
		Thread.Sleep(45);
		return stopwatch.Elapsed.TotalMilliseconds;
	}

	private static void Query(SerialPort port,string command)
	{
		port.DiscardInBuffer();
		byte[] request=Ascii.GetBytes(command);
		byte[] response=new byte[5];
		Stopwatch stopwatch=Stopwatch.StartNew();
		port.Write(request,0,request.Length);
		int offset=0;
		while(offset<response.Length)
		{
			offset+=port.Read(response,offset,response.Length-offset);
		}
		stopwatch.Stop();
		string text=Ascii.GetString(response);
		string hex=Convert.ToHexString(response);
		Console.WriteLine(
			$"{port.PortName} TX {command} RX '{text}' hex={hex} - "+
			$"{stopwatch.Elapsed.TotalMilliseconds:0.000} ms");
		Thread.Sleep(45);
	}

	private static void EmergencyOff(SerialPort port)
	{
		if(!port.IsOpen)
		{
			return;
		}
		try
		{
			Write(port,"OUT0");
		}
		catch(Exception exception)
		{
			Console.Error.WriteLine(
				$"{port.PortName} awaryjne OUT0 nie powiodło się: {exception.Message}");
		}
	}
}
