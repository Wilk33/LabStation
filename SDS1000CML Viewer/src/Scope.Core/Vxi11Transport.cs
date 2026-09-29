using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

namespace Scope.Core;

public sealed class Xdr

{

	private readonly MemoryStream stream;
	public Xdr()

	{

		stream=new();

	}

	public Xdr(byte[] bytes)

	{

		stream=new(bytes, false);

	}

	public void Put(uint value)

	{

		Span<byte> b=stackalloc byte[4];
		BinaryPrimitives.WriteUInt32BigEndian(b, value);
		stream.Write(b);

	}

	public uint Get()

	{

		Span<byte> b=stackalloc byte[4];
		stream.ReadExactly(b);
		return BinaryPrimitives.ReadUInt32BigEndian(b);

	}

	public void PutBytes(byte[] b)

	{

		Put((uint)b.Length);
		stream.Write(b);
		for (int i=b.Length; i%4 != 0; i++)
			stream.WriteByte(0);

	}

	public byte[] GetBytes()

	{

		uint size=Get();
		if (size > Scpi.MaxMessage)
			throw new InvalidDataException("Zbyt duży blok RPC.");
		byte[] b=new byte[size];
		stream.ReadExactly(b);
		int padding=(4-(int)size%4)%4;
		Span<byte> pad=stackalloc byte[3];
		stream.ReadExactly(pad[..padding]);
		return b;

	}

	public byte[] Bytes() => stream.ToArray();

}


public sealed class RpcConnection : IDisposable

{

	private readonly TcpClient client=new();
	private uint sequence;
	public RpcConnection(string host, int port)

	{

		try

		{

			client.ConnectAsync(host, port).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
			client.ReceiveTimeout=15000;
			client.SendTimeout=15000;
			client.NoDelay=true;

		}

		catch
		{
			client.Dispose();
			throw;
		}


	}

	public Xdr Call(uint program, uint version, uint procedure, Action<Xdr> arguments)

	{

		Xdr call=new();
		uint id=++sequence;
		foreach (uint n in new[]
{
id, 0u, 2u, program, version, procedure, 0u, 0u, 0u, 0u
})
			call.Put(n);
		arguments(call);
		byte[] payload=call.Bytes();
		NetworkStream stream=client.GetStream();
		byte[] header=new byte[4];
		BinaryPrimitives.WriteUInt32BigEndian(header, 0x80000000u|(uint)payload.Length);
		stream.Write(header);
		stream.Write(payload);
		using MemoryStream reply=new();
		for (int fragments=0; ; fragments++)

		{

			stream.ReadExactly(header);
			uint marker=BinaryPrimitives.ReadUInt32BigEndian(header);
			int count=(int)(marker&0x7fffffffu);
			if (fragments > 4096 || count > Scpi.MaxMessage || reply.Length+count > Scpi.MaxMessage)
				throw new InvalidDataException("Zbyt duża odpowiedź RPC.");
			byte[] part=new byte[count];
			stream.ReadExactly(part);
			reply.Write(part);
			if ((marker&0x80000000u) != 0)
				break;

		}

		Xdr result=new(reply.ToArray());
		if (result.Get() != id || result.Get() != 1 || result.Get() != 0)
			throw new IOException("Serwer odrzucił wywołanie RPC.");
		result.Get();
		result.GetBytes();
		if (result.Get() != 0)
			throw new IOException("Błąd procedury RPC.");
		return result;

	}

	public void Dispose() => client.Dispose();

}


public sealed class Vxi11Transport : IInstrumentTransport

{

	private const uint Program=395183;
	private readonly RpcConnection core;
	private readonly uint link;
	private readonly int maxWrite;
	private bool disposed;
	public Vxi11Transport(string host, int mapperPort=111)

	{

		using (RpcConnection mapper=new(host, mapperPort))

		{

			Xdr port=mapper.Call(100000, 2, 3, x =>

{

	x.Put(Program);
	x.Put(1);
	x.Put(6);
	x.Put(0);

});
			uint number=port.Get();
			if (number == 0 || number > 65535)
				throw new IOException("VXI-11 niedostępne pod tym adresem.");
			core=new(host, (int)number);

		}

		try

		{

			Xdr response=core.Call(Program, 1, 10, x =>

{

	x.Put((uint)Random.Shared.Next(1, int.MaxValue));
	x.Put(0);
	x.Put(10000);
	x.PutBytes(Encoding.ASCII.GetBytes("inst0"));

});
			Check(response.Get());
			link=response.Get();
			response.Get();
			uint size=response.Get();
			maxWrite=(int)Math.Clamp(size, 1u, 1048576u);

		}

		catch
		{
			core.Dispose();
			throw;
		}


	}

	private static void Check(uint error)

	{

		if (error != 0)
			throw new IOException($"Błąd VXI-11: {error}"+(error == 15 ? " (przekroczony czas odpowiedzi)." : "."));

	}

	public void Write(string command)

	{

		ObjectDisposedException.ThrowIf(disposed, this);
		byte[] data=Encoding.ASCII.GetBytes(command+"\n");
		for (int offset=0; offset < data.Length;)

		{

			int count=Math.Min(maxWrite, data.Length-offset);
			byte[] chunk=data.AsSpan(offset, count).ToArray();
			uint flags=offset+count == data.Length ? 8u : 0u;
			Xdr reply=core.Call(Program, 1, 11, x =>

{

	x.Put(link);
	x.Put(10000);
	x.Put(10000);
	x.Put(flags);
	x.PutBytes(chunk);

});
			Check(reply.Get());
			if (reply.Get() != count)
				throw new IOException("Niepełny zapis VXI-11.");
			offset+=count;

		}


	}

	public byte[] Query(string command)

	{

		Write(command);
		using MemoryStream output=new();
		while (true)

		{

			Xdr reply=core.Call(Program, 1, 12, x =>

{

	x.Put(link);
	x.Put(1048576);
	x.Put(10000);
	x.Put(10000);
	x.Put(0);
	x.Put(0);

});
			Check(reply.Get());
			uint reason=reply.Get();
			byte[] data=reply.GetBytes();
			if (data.Length == 0 && (reason&4) == 0)
				throw new IOException("Pusta odpowiedź VXI-11.");
			if (output.Length+data.Length > Scpi.MaxMessage)
				throw new IOException("Przekroczony limit danych.");
			output.Write(data);
			if ((reason&4) != 0)
				return output.ToArray();
			if ((reason&2) != 0)
				throw new IOException("Nieoczekiwane zakończenie transferu znakiem końca.");

		}


	}

	public void Dispose()

	{

		if (disposed)
			return;
		disposed=true;
		try

		{

			core.Call(Program, 1, 23, x => x.Put(link));

		}

		catch (Exception)
		{

		}

		core.Dispose();

	}


}
