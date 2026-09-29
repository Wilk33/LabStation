using Scope.Core;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Buffers.Binary;

internal sealed class ScriptedTransport(byte[] waveform) : IInstrumentTransport

{

	public string Mode="AUTO";
	public string SampleStatus="Trig'd";
	public List<string> Commands=[];
	public void Write(string command) => Commands.Add(command);
	public byte[] Query(string command)

	{

		Commands.Add(command);
		string text=command switch

		{

			"*IDN?" => "SIGLENT,SDS1102CML+,TEST,6.01",
			"TRMD?" => "TRMD "+Mode,
			"SAST?" => "SAST "+SampleStatus,
			"C1:PAVA? PKPK,RMS,FREQ,MIN,MAX,DUTY" => "C1:PAVA PKPK,4.08E+00V,RMS,1.36E+00V,FREQ,8.00E+03Hz,MIN,-2.08E+00V,MAX,2.00E+00V,DUTY,51.36%",
			"C2:PAVA? PKPK,RMS,FREQ,MIN,MAX,DUTY" => "C2:PAVA PKPK,3.12E+00V,RMS,2.16E+00V,FREQ,1.00E+03Hz,MIN,0.00E+00V,MAX,3.12E+00V,DUTY,50.00%",
			"C1:TRA?" => "C1:TRA ON",
			"C2:TRA?" => "C2:TRA ON",
			_ => ""

		};
		return command.EndsWith("WF? ALL") ? waveform : Encoding.ASCII.GetBytes(text);

	}

	public void Dispose()

	{


	}


}

internal sealed class LoopbackInstrument : IDisposable

{

	private readonly TcpListener mapper=new(IPAddress.Loopback, 0), core=new(IPAddress.Loopback, 0);
	private readonly Task task;
	private TcpClient? client;
	public int Port => (mapper.LocalEndpoint as IPEndPoint)!.Port;
	public string LastCommand="";
	public LoopbackInstrument()

	{

		mapper.Start();
		core.Start();
		task=Task.Run(Run);

	}

	private static Xdr Read(NetworkStream stream)

	{

		byte[] header=new byte[4];
		stream.ReadExactly(header);
		int size=(int)(BinaryPrimitives.ReadUInt32BigEndian(header)&0x7fffffff);
		byte[] bytes=new byte[size];
		stream.ReadExactly(bytes);
		return new(bytes);

	}

	private static void Send(NetworkStream stream, uint id, Action<Xdr> body)

	{

		Xdr reply=new();
		foreach (uint value in new[]
{
id, 1u, 0u, 0u, 0u, 0u
})
			reply.Put(value);
		body(reply);
		byte[] b=reply.Bytes();
		// Split the RPC record into two fragments and TCP writes into small pieces.
		for (int offset=0; offset < b.Length;)

		{

			int length=Math.Min(7, b.Length-offset);
			byte[] marker=new byte[4];
			BinaryPrimitives.WriteUInt32BigEndian(marker, (uint)length|(offset+length == b.Length ? 0x80000000u : 0u));
			stream.Write(marker);
			stream.Write(b, offset, length);
			offset+=length;

		}


	}

	private void Run()

	{

		using (TcpClient portClient=mapper.AcceptTcpClient())

		{

			NetworkStream stream=portClient.GetStream();
			Xdr call=Read(stream);
			uint id=call.Get();
			Send(stream, id, x => x.Put((uint)(core.LocalEndpoint as IPEndPoint)!.Port));

		}

		client=core.AcceptTcpClient();
		using (client)

		{

			NetworkStream stream=client.GetStream();
			using MemoryStream commands=new();
			int reads=0;
			while (true)

			{

				Xdr call=Read(stream);
				uint id=call.Get();
				call.Get();
				call.Get();
				call.Get();
				call.Get();
				uint procedure=call.Get();
				call.Get();
				call.GetBytes();
				call.Get();
				call.GetBytes();
				if (procedure == 10)

				{

					Send(stream, id, x =>
{
	x.Put(0);
	x.Put(7);
	x.Put(0);
	x.Put(4);
});

				}

				else if (procedure == 11)

				{

					call.Get();
					call.Get();
					call.Get();
					uint flags=call.Get();
					byte[] bytes=call.GetBytes();
					commands.Write(bytes);
					if ((flags&8) != 0)

					{

						LastCommand=Encoding.ASCII.GetString(commands.ToArray());
						commands.SetLength(0);

					}

					Send(stream, id, x =>
{
	x.Put(0);
	x.Put((uint)bytes.Length);
});

				}

				else if (procedure == 12)

				{

					bool last=reads++ > 0;
					Send(stream, id, x =>
{
	x.Put(0);
	x.Put(last ? 4u : 1u);
	x.PutBytes(last ? [42, 0, 1] : [0, 10, 13, 255]);
});

				}

				else if (procedure == 23)

				{

					Send(stream, id, x => x.Put(0));
					break;

				}

				else
					throw new Exception("Unexpected VXI procedure");

			}


		}


	}

	public void Dispose()

	{

		mapper.Stop();
		core.Stop();
		client?.Dispose();
		try

		{

			task.Wait(TimeSpan.FromSeconds(2));

		}

		catch (AggregateException)
		{

		}


	}


}
