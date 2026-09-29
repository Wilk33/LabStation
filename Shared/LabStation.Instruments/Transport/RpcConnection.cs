using System.Buffers.Binary;
using System.Net.Sockets;

namespace LabStation.Instruments.Transport;

internal sealed class RpcConnection : IDisposable
{
	private readonly TcpClient client=new();
	private readonly int maximumMessageSize;
	private uint sequence;

	public RpcConnection(
		string host,
		int port,
		TimeSpan connectTimeout,
		TimeSpan ioTimeout,
		int maximumMessageSize)
	{
		this.maximumMessageSize=maximumMessageSize;
		try
		{
			client.ConnectAsync(host,port)
				.WaitAsync(connectTimeout)
				.GetAwaiter()
				.GetResult();
			int timeout=(int)Math.Clamp(ioTimeout.TotalMilliseconds,1,int.MaxValue);
			client.ReceiveTimeout=timeout;
			client.SendTimeout=timeout;
			client.NoDelay=true;
		}
		catch
		{
			client.Dispose();
			throw;
		}
	}

	public Xdr Call(uint program,uint version,uint procedure,Action<Xdr> arguments)
	{
		Xdr call=new(maximumMessageSize);
		uint id=++sequence;
		foreach(uint value in new[]{id,0u,2u,program,version,procedure,0u,0u,0u,0u})
		{
			call.Put(value);
		}
		arguments(call);
		byte[] payload=call.Bytes();
		NetworkStream network=client.GetStream();
		byte[] header=new byte[4];
		BinaryPrimitives.WriteUInt32BigEndian(header,0x80000000u|(uint)payload.Length);
		network.Write(header);
		network.Write(payload);
		using MemoryStream reply=new();
		for(int fragments=0;;fragments++)
		{
			network.ReadExactly(header);
			uint marker=BinaryPrimitives.ReadUInt32BigEndian(header);
			int count=(int)(marker&0x7fffffffu);
			if(fragments>4096 || count>maximumMessageSize || reply.Length+count>maximumMessageSize)
			{
				throw new InvalidDataException("Zbyt duża odpowiedź RPC.");
			}
			byte[] part=new byte[count];
			network.ReadExactly(part);
			reply.Write(part);
			if((marker&0x80000000u) != 0)
			{
				break;
			}
		}
		Xdr result=new(reply.ToArray(),maximumMessageSize);
		if(result.Get() != id || result.Get() != 1 || result.Get() != 0)
		{
			throw new IOException("Serwer odrzucił wywołanie RPC.");
		}
		result.Get();
		result.GetBytes();
		if(result.Get() != 0)
		{
			throw new IOException("Błąd procedury RPC.");
		}
		return result;
	}

	public void Dispose()=>client.Dispose();
}
