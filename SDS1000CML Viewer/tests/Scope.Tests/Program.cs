using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Scope.Core;
using LabStation.Instruments.Scpi;
using LabStation.Instruments.Scheduling;
using LabStation.Instruments.Transport;

int failed=0;
void Test(string name, Action test)

{

	try

	{

		test();
		Console.WriteLine($"PASS {name}");

	}

	catch (Exception ex)
	{
		failed++;
		Console.WriteLine($"FAIL {name}: {ex.Message}");
	}


}

void Eq(double want, double got)

{

	if (Math.Abs(want-got) > 1e-6)
		throw new Exception($"Expected {want}, got {got}");

}

void Reject(Action action)

{

	try

	{

		action();

	}

	catch (InvalidDataException)
	{
		return;
	}

	throw new Exception("Invalid data accepted");

}

byte[] Fixture()

{

	byte[] b=new byte[350];
	Encoding.ASCII.GetBytes("WAVEDESC").CopyTo(b, 0);
	BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(34), 1);
	BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(36), 346);
	BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(60), 4);
	BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(116), 4);
	BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(156), 0.02f);
	BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(160), 0.5f);
	BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(176), 0.001f);
	BinaryPrimitives.WriteDoubleLittleEndian(b.AsSpan(180), -0.002);
	b[346]=0;
	b[347]=25;
	b[348]=255;
	b[349]=128;
	return [.. Encoding.ASCII.GetBytes("C1:WF ALL,#9000000350"), .. b, 10, 10];

}


Test("Binary transfer preserves embedded LF and CR", () =>

{

	byte[] got=Scpi.Block([.. Encoding.ASCII.GetBytes("C1:WF DAT2,#14"), 0, 10, 13, 255, 10, 10]);
	if (!got.SequenceEqual(new byte[]
{
0, 10, 13, 255
}))
		throw new Exception("Payload changed");

});
Test("Truncated and oversized binary blocks rejected", () =>

{

	Reject(() => Scpi.Block(Encoding.ASCII.GetBytes("#15abc")));
	Reject(() => Scpi.Block(Encoding.ASCII.GetBytes("#9999999999")));

});
Test("SCPI prefix and engineering units", () =>

{

	Eq(0.002, Scpi.Number("C1:VDIV 2.00mV"));
	Eq(1e9, Scpi.Number("SARA 1.00GSa/s"));
	Eq(-5e-9, Scpi.Number("TRDL -5.00E-09S"));

});
Test("Wave descriptor decodes signed voltage and acquisition time", () =>

{

	Waveform w=Waveform.Decode(1, Fixture());
	Eq(-0.5, w.Volts[0]);
	Eq(0, w.Volts[1]);
	Eq(-0.52, w.Volts[2]);
	Eq(-3.06, w.Volts[3]);
	Eq(0.001, w.Interval);
	Eq(-0.002, w.Start);

});
Test("Incomplete wave descriptor rejected", () => Reject(() => Waveform.Decode(1, Encoding.ASCII.GetBytes("#13abc"))));
Test("CSV is culture independent and preserves each channel time axis", () =>

{

	CultureInfo.CurrentCulture=new CultureInfo("pl-PL");
	using StringWriter s=new();
	Csv.Write(s, [new(1, [0.5, -0.25], 0.001, -0.002, DateTimeOffset.UnixEpoch), new(2, [1.25], 0.002, 0, DateTimeOffset.UnixEpoch)]);
	string csv=s.ToString().Replace("\r", "");
	if (!csv.Contains("CH1,-0.002,0.5") || !csv.Contains("CH1,-0.001,-0.25") || !csv.Contains("CH2,0,1.25"))
		throw new Exception(csv);

});
Test("Empty CSV contains the header and no samples", () =>

{

	using StringWriter s=new();
	Csv.Write(s, []);
	string csv=s.ToString().Replace("\r", "");
	if (csv != "channel,time_s,voltage_V,captured_utc\n")
		throw new Exception(csv);

});
Test("Capture never starts, stops, autoscales or enables oscilloscope channels", () =>

{

	using ScriptedTransport transport=new(Fixture());
	using ScopeClient client=new(transport);
	client.Initialize();
	Waveform[] w=client.Capture([1, 2]);
	if (w.Length != 2 || w[0].Volts.Length != 4)
		throw new Exception("Capture data missing");
	if (transport.Commands.Any(c => !c.Contains('?') && c != "WFSU SP,1,NP,0,FP,0"))
		throw new Exception("Unexpected setting changed");

});
Test("Start without observed running mode uses continuous AUTO instead of single ARM", () =>

{

	using ScriptedTransport transport=new(Fixture())

	{

		Mode="STOP"

	};
	using ScopeClient client=new(transport);
	client.Initialize();
	client.Start();
	if (transport.Commands[^1] != "TRMD AUTO")
		throw new Exception("Start is not continuous");

});
Test("Stop and Start preserve NORM trigger mode", () =>

{

	using ScriptedTransport transport=new(Fixture())

	{

		Mode="NORM"

	};
	using ScopeClient client=new(transport);
	client.Initialize();
	client.Stop();
	transport.Mode="STOP";
	client.Start();
	if (transport.Commands[^1] != "TRMD NORM")
		throw new Exception("Trigger mode not restored");

});
Test("Acquisition status maps physical SAST responses to Start and Stop", () =>

{

	using ScriptedTransport transport=new(Fixture())
	{
		SampleStatus="Stop"
	};
	using ScopeClient client=new(transport);
	client.Initialize();
	if (client.AcquisitionStatus() != AcquisitionState.Stop)
		throw new Exception("SAST Stop not recognized");
	transport.SampleStatus="Trig'd";
	if (client.AcquisitionStatus() != AcquisitionState.Start)
		throw new Exception("SAST Trig'd not recognized");

});
Test("PAVA measurements parse both channels with units and duty percent", () =>

{

	using ScriptedTransport transport=new(Fixture());
	using ScopeClient client=new(transport);
	client.Initialize();
	ChannelMeasurements[] values=client.Measurements([1, 2]);
	if (values.Length != 2)
		throw new Exception("Missing channel measurements");
	Eq(4.08,values[0].Vpp!.Value);
	Eq(1.36,values[0].Vrms!.Value);
	Eq(8000,values[0].Frequency!.Value);
	Eq(-2.08,values[0].Minimum!.Value);
	Eq(2.00,values[0].Maximum!.Value);
	Eq(51.36,values[0].DutyPercent!.Value);
	Eq(50.00,values[1].DutyPercent!.Value);

});Test("Unavailable PAVA values remain visible as missing measurements", () =>

{

	ChannelMeasurements value=ChannelMeasurements.Parse(1,"C1:PAVA PKPK,****,RMS,1.00V,FREQ,****,MIN,-1.00V,MAX,1.00V,DUTY,****");
	if(value.Vpp != null || value.Frequency != null || value.DutyPercent != null || value.Vrms != 1)
		throw new Exception("Unavailable measurement was not preserved as missing");

});Test("Command waits for active preview and blocks the next preview", () =>

{

	using SerializedOperationGate queue=new();
	using ManualResetEventSlim previewEntered=new(), releasePreview=new();
	List<string> order=[];
	Task first=queue.TryRunBackgroundAsync(async () =>
	{
		lock (order)
			order.Add("preview-1-start");
		previewEntered.Set();
		await Task.Run(releasePreview.Wait);
		lock (order)
			order.Add("preview-1-end");
	});
	if (!previewEntered.Wait(TimeSpan.FromSeconds(2)))
		throw new Exception("Preview did not start");
	Task command=queue.RunForegroundAsync(() =>
	{
		lock (order)
			order.Add("stop");
		return Task.CompletedTask;
	});
	Task<bool> second=queue.TryRunBackgroundAsync(() =>
	{
		lock (order)
			order.Add("preview-2");
		return Task.CompletedTask;
	});
	releasePreview.Set();
	Task.WaitAll(first, command, second);
	if (second.Result || !order.SequenceEqual(new[] { "preview-1-start", "preview-1-end", "stop" }))
		throw new Exception(string.Join(",", order));

});
Test("Real VXI-11 TCP session handles RPC fragments and multi-part binary read", () =>

{

	using LoopbackInstrument server=new();
	using Vxi11Transport transport=new("127.0.0.1", server.Port);
	byte[] response=transport.Query("C1:WF? ALL");
	if (!response.SequenceEqual(new byte[]
{
0, 10, 13, 255, 42, 0, 1
}))
		throw new Exception("TCP payload changed");
	if (server.LastCommand != "C1:WF? ALL\n")
		throw new Exception("Write chunk assembly failed");

});
Test("VXI-11 transport is provided by the shared instrument library", () =>

{

	if (typeof(Vxi11Transport).Assembly.GetName().Name != "LabStation.Instruments")
		throw new Exception("VXI-11 transport is still local to Scope.Core");

});
Test("Supported model predicate belongs to the oscilloscope",() =>
{
	if(!ScopeClient.IsSupported(new ScpiIdentity(
		"SIGLENT",
		"SDS1102CML+",
		"TEST",
		"6.01")))
	{
		throw new Exception("Supported SDS1102CML+ was rejected");
	}
	if(ScopeClient.IsSupported(new ScpiIdentity(
		"SIGLENT",
		"SDG1032X",
		"TEST",
		"1")) ||
		ScopeClient.IsSupported(new ScpiIdentity(
			"OTHER",
			"SDS1102CML+",
			"TEST",
			"1")))
	{
		throw new Exception("Unsupported identity was accepted");
	}
});

Console.WriteLine($"Failures: {failed}");
return failed == 0 ? 0 : 1;
