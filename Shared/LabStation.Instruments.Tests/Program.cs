using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using LabStation.Instruments.Scpi;
using LabStation.Instruments.Scheduling;
using LabStation.Instruments.Transport;

int passed=0;
int failed=0;

void Test(string name,Action action)
{
	try
	{
		action();
		passed++;
		Console.WriteLine("PASS "+name);
	}
	catch(Exception exception)
	{
		failed++;
		Console.WriteLine("FAIL "+name+": "+exception);
	}
}

void Equal<T>(T expected,T actual)
{
	if(!EqualityComparer<T>.Default.Equals(expected,actual))
	{
		throw new Exception($"Oczekiwano {expected}, otrzymano {actual}");
	}
}

Test("Parser IDN rozdziela pola bez decydowania o modelu",()=>
{
	ScpiIdentity identity=ScpiIdentity.Parse(" SIGLENT , SDS1102CML+ , 1234 , 6.01 ");
	Equal("SIGLENT",identity.Manufacturer);
	Equal("SDS1102CML+",identity.Model);
	Equal("1234",identity.SerialNumber);
	Equal("6.01",identity.Firmware);
});

Test("Połączenie SCPI zachowuje dane binarne i czyści terminator tekstu",()=>
{
	using ScriptedTransport transport=new([0,10,13,255]);
	using ScpiConnection connection=new(transport,false);
	if(!connection.QueryBytes("C1:WF? ALL").SequenceEqual(new byte[]{0,10,13,255}))
	{
		throw new Exception("Dane binarne zostały zmienione");
	}
	transport.Response=Encoding.ASCII.GetBytes("SIGLENT,MODEL\r\n");
	Equal("SIGLENT,MODEL",connection.QueryText("*IDN?"));
});

Test("Kolejka zastępuje starszą wartość i zachowuje priorytety",()=>
{
	LatestRequestQueue<string> queue=new();
	ScheduledRequest<string> older=queue.EnqueueLatest("C1:FRQ","FRQ 100");
	queue.EnqueueLatest("C1:FRQ","FRQ 200");
	queue.EnqueuePriority("OUTP OFF");
	queue.EnqueueOrdered("OUTP ON");
	Equal("OUTP OFF",queue.TakeNext().Value);
	Equal("FRQ 200",queue.TakeNext().Value);
	Equal("OUTP ON",queue.TakeNext().Value);
	if(!older.Completion.IsCanceled)
	{
		throw new Exception("Zastąpione żądanie nie zostało anulowane");
	}
});

Test("Brama przepuszcza polecenie przed następnym podglądem",()=>
{
	SerializedOperationGate gate=new();
	TaskCompletionSource previewStarted=new(TaskCreationOptions.RunContinuationsAsynchronously);
	TaskCompletionSource finishPreview=new(TaskCreationOptions.RunContinuationsAsynchronously);
	Task first=gate.TryRunBackgroundAsync(async()=>
	{
		previewStarted.SetResult();
		await finishPreview.Task;
	});
	previewStarted.Task.GetAwaiter().GetResult();
	bool commandRan=false;
	Task command=gate.RunForegroundAsync(()=>
	{
		commandRan=true;
		return Task.CompletedTask;
	});
	Thread.Sleep(20);
	Equal(false,commandRan);
	Equal(false,gate.TryRunBackgroundAsync(()=>Task.CompletedTask).GetAwaiter().GetResult());
	finishPreview.SetResult();
	Task.WaitAll(first,command);
	Equal(true,commandRan);
});

Test("VXI-11 obsługuje fragmenty RPC i wieloczęściowy odczyt",()=>
{
	using LoopbackVxi11Server server=new();
	using Vxi11Transport transport=new("127.0.0.1",server.MapperPort);
	byte[] response=transport.Query("C1:WF? ALL");
	if(!response.SequenceEqual(new byte[]{0,10,13,255,42,0,1}))
	{
		throw new Exception("Odpowiedź VXI-11 została zmieniona");
	}
	Equal("C1:WF? ALL\n",server.LastCommand);
});

Test("Socket SCPI odczytuje tekst i blok binarny",()=>
{
	using LoopbackScpiServer server=new();
	using TcpScpiTransport transport=new("127.0.0.1",server.Port);
	Equal("READY\n",Encoding.ASCII.GetString(transport.Query("TEXT?")));
	if(!transport.Query("BINARY?").SequenceEqual(new byte[]{1,10,13,255}))
	{
		throw new Exception("Blok binarny socketu został zmieniony");
	}
	Equal("DONE\n",Encoding.ASCII.GetString(transport.Query("AFTER?")));
	server.Wait();
	Equal("TEXT?\nBINARY?\nAFTER?\n",server.Commands);
});

Console.WriteLine($"Wynik: {passed} zaliczonych, {failed} niezaliczonych");
return failed == 0 ? 0 : 1;

sealed class ScriptedTransport(byte[] response) : IInstrumentTransport
{
	public byte[] Response { get;set; }=response;
	public void Write(string command)
	{
	}
	public byte[] Query(string command)=>Response;
	public void Dispose()
	{
	}
}

sealed class LoopbackVxi11Server : IDisposable
{
	private const uint Program=395183;
	private readonly TcpListener mapper=new(IPAddress.Loopback,0);
	private readonly TcpListener core=new(IPAddress.Loopback,0);
	private readonly Task mapperTask;
	private readonly Task coreTask;
	private readonly MemoryStream written=new();

	public LoopbackVxi11Server()
	{
		core.Start();
		mapper.Start();
		MapperPort=((IPEndPoint)mapper.LocalEndpoint).Port;
		mapperTask=Task.Run(ServeMapper);
		coreTask=Task.Run(ServeCore);
	}

	public int MapperPort { get; }
	public string LastCommand=>Encoding.ASCII.GetString(written.ToArray());

	public void Dispose()
	{
		mapper.Stop();
		core.Stop();
		try
		{
			Task.WaitAll([mapperTask,coreTask],TimeSpan.FromSeconds(2));
		}
		catch(AggregateException)
		{
		}
		written.Dispose();
	}

	private void ServeMapper()
	{
		using TcpClient client=mapper.AcceptTcpClient();
		NetworkStream network=client.GetStream();
		(uint id,uint procedure,XdrReader call)=ReadCall(network);
		EqualProcedure(3,procedure);
		call.Get();
		call.Get();
		call.Get();
		call.Get();
		SendReply(network,id,writer=>writer.Put((uint)((IPEndPoint)core.LocalEndpoint).Port),false);
	}

	private void ServeCore()
	{
		using TcpClient client=core.AcceptTcpClient();
		NetworkStream network=client.GetStream();
		int reads=0;
		while(true)
		{
			(uint id,uint procedure,XdrReader call)=ReadCall(network);
			switch(procedure)
			{
				case 10:
					call.Get();
					call.Get();
					call.Get();
					call.GetBytes();
					SendReply(network,id,writer=>
					{
						writer.Put(0);
						writer.Put(123);
						writer.Put(0);
						writer.Put(4);
					},false);
					break;
				case 11:
					call.Get();
					call.Get();
					call.Get();
					call.Get();
					byte[] part=call.GetBytes();
					written.Write(part);
					SendReply(network,id,writer=>
					{
						writer.Put(0);
						writer.Put((uint)part.Length);
					},false);
					break;
				case 12:
					for(int index=0;index<6;index++)
					{
						call.Get();
					}
					bool last=reads++>0;
					SendReply(network,id,writer=>
					{
						writer.Put(0);
						writer.Put(last ? 4u : 1u);
						writer.PutBytes(last ? [42,0,1] : [0,10,13,255]);
					},true);
					break;
				case 23:
					call.Get();
					SendReply(network,id,writer=>writer.Put(0),false);
					return;
				default:
					throw new InvalidDataException("Nieoczekiwana procedura VXI-11: "+procedure);
			}
		}
	}

	private static (uint Id,uint Procedure,XdrReader Arguments) ReadCall(NetworkStream network)
	{
		XdrReader call=new(ReadRecord(network));
		uint id=call.Get();
		if(call.Get() != 0 || call.Get() != 2)
		{
			throw new InvalidDataException("Nieprawidłowe wywołanie RPC");
		}
		call.Get();
		call.Get();
		uint procedure=call.Get();
		call.Get();
		call.GetBytes();
		call.Get();
		call.GetBytes();
		return (id,procedure,call);
	}

	private static byte[] ReadRecord(NetworkStream network)
	{
		using MemoryStream result=new();
		byte[] markerBytes=new byte[4];
		while(true)
		{
			network.ReadExactly(markerBytes);
			uint marker=BinaryPrimitives.ReadUInt32BigEndian(markerBytes);
			byte[] part=new byte[marker&0x7fffffffu];
			network.ReadExactly(part);
			result.Write(part);
			if((marker&0x80000000u) != 0)
			{
				return result.ToArray();
			}
		}
	}

	private static void SendReply(NetworkStream network,uint id,Action<XdrWriter> body,bool split)
	{
		XdrWriter reply=new();
		reply.Put(id);
		reply.Put(1);
		reply.Put(0);
		reply.Put(0);
		reply.PutBytes([]);
		reply.Put(0);
		body(reply);
		byte[] bytes=reply.Bytes();
		if(!split)
		{
			SendFragment(network,bytes,true);
			return;
		}
		int middle=bytes.Length/2;
		SendFragment(network,bytes[..middle],false);
		SendFragment(network,bytes[middle..],true);
	}

	private static void SendFragment(NetworkStream network,byte[] bytes,bool last)
	{
		byte[] marker=new byte[4];
		BinaryPrimitives.WriteUInt32BigEndian(marker,(uint)bytes.Length|(last ? 0x80000000u : 0));
		network.Write(marker);
		network.Write(bytes);
	}

	private static void EqualProcedure(uint expected,uint actual)
	{
		if(expected != actual)
		{
			throw new InvalidDataException($"Oczekiwano procedury {expected}, otrzymano {actual}");
		}
	}
}

sealed class LoopbackScpiServer : IDisposable
{
	private readonly TcpListener listener=new(IPAddress.Loopback,0);
	private readonly Task task;

	public LoopbackScpiServer()
	{
		listener.Start();
		Port=((IPEndPoint)listener.LocalEndpoint).Port;
		task=Task.Run(Serve);
	}

	public int Port { get; }
	public string Commands { get;private set; }=string.Empty;

	public void Wait()
	{
		if(!task.Wait(TimeSpan.FromSeconds(5)))
		{
			throw new TimeoutException("Serwer SCPI nie zakończył pracy");
		}
	}

	public void Dispose()
	{
		listener.Stop();
		try
		{
			task.Wait(TimeSpan.FromSeconds(1));
		}
		catch(AggregateException)
		{
		}
	}

	private void Serve()
	{
		using TcpClient client=listener.AcceptTcpClient();
		NetworkStream stream=client.GetStream();
		using StreamReader reader=new(stream,Encoding.ASCII,false,1024,true);
		string? first=reader.ReadLine();
		Commands+=first+"\n";
		byte[] text=Encoding.ASCII.GetBytes("READY\n");
		stream.Write(text);
		string? second=reader.ReadLine();
		Commands+=second+"\n";
		byte[] binary=[(byte)'#',(byte)'1',(byte)'4',1,10,13,255,(byte)'\n'];
		stream.Write(binary);
		string? third=reader.ReadLine();
		Commands+=third+"\n";
		byte[] after=Encoding.ASCII.GetBytes("DONE\n");
		stream.Write(after);
	}
}

sealed class XdrWriter
{
	private readonly MemoryStream stream=new();
	public void Put(uint value)
	{
		Span<byte> bytes=stackalloc byte[4];
		BinaryPrimitives.WriteUInt32BigEndian(bytes,value);
		stream.Write(bytes);
	}
	public void PutBytes(byte[] bytes)
	{
		Put((uint)bytes.Length);
		stream.Write(bytes);
		for(int index=bytes.Length;index%4 != 0;index++)
		{
			stream.WriteByte(0);
		}
	}
	public byte[] Bytes()=>stream.ToArray();
}

sealed class XdrReader(byte[] bytes)
{
	private readonly MemoryStream stream=new(bytes,false);
	public uint Get()
	{
		Span<byte> value=stackalloc byte[4];
		stream.ReadExactly(value);
		return BinaryPrimitives.ReadUInt32BigEndian(value);
	}
	public byte[] GetBytes()
	{
		uint size=Get();
		byte[] value=new byte[size];
		stream.ReadExactly(value);
		int padding=(4-(int)size%4)%4;
		Span<byte> pad=stackalloc byte[3];
		stream.ReadExactly(pad[..padding]);
		return value;
	}
}
