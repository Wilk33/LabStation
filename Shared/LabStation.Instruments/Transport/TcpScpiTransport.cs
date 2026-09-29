using System.Net.Sockets;
using System.Text;

namespace LabStation.Instruments.Transport;

public sealed class TcpScpiTransport : IInstrumentTransport
{
	private readonly object gate=new();
	private readonly TcpClient client=new();
	private readonly NetworkStream stream;
	private readonly string terminator;
	private readonly int maximumResponseBytes;
	private bool disposed;

	public TcpScpiTransport(
		string host,
		int port=5025,
		TimeSpan? connectTimeout=null,
		TimeSpan? ioTimeout=null,
		int maximumResponseBytes=32*1024*1024,
		string terminator="\n")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(host);
		this.terminator=terminator;
		this.maximumResponseBytes=maximumResponseBytes;
		TimeSpan connect=connectTimeout ?? TimeSpan.FromSeconds(5);
		TimeSpan io=ioTimeout ?? TimeSpan.FromSeconds(15);
		try
		{
			client.ConnectAsync(host,port).WaitAsync(connect).GetAwaiter().GetResult();
			int timeout=(int)Math.Clamp(io.TotalMilliseconds,1,int.MaxValue);
			client.ReceiveTimeout=timeout;
			client.SendTimeout=timeout;
			client.NoDelay=true;
			stream=client.GetStream();
		}
		catch
		{
			client.Dispose();
			throw;
		}
	}

	public void Write(string command)
	{
		lock(gate)
		{
			WriteCore(command);
		}
	}

	public byte[] Query(string command)
	{
		lock(gate)
		{
			WriteCore(command);
			int first=stream.ReadByte();
			if(first<0)
			{
				throw new EndOfStreamException("Połączenie SCPI zostało zamknięte.");
			}
			if(first == '#')
			{
				return ReadDefiniteBlock();
			}
			using MemoryStream response=new();
			response.WriteByte((byte)first);
			while(first != '\n')
			{
				first=stream.ReadByte();
				if(first<0)
				{
					throw new EndOfStreamException("Niepełna odpowiedź SCPI.");
				}
				response.WriteByte((byte)first);
				if(response.Length>maximumResponseBytes)
				{
					throw new IOException("Przekroczony limit danych SCPI.");
				}
			}
			return response.ToArray();
		}
	}

	public void Dispose()
	{
		lock(gate)
		{
			if(disposed)
			{
				return;
			}
			disposed=true;
			stream.Dispose();
			client.Dispose();
		}
	}

	private void WriteCore(string command)
	{
		ObjectDisposedException.ThrowIf(disposed,this);
		byte[] bytes=Encoding.ASCII.GetBytes(command+terminator);
		stream.Write(bytes);
	}

	private byte[] ReadDefiniteBlock()
	{
		int digits=stream.ReadByte()-'0';
		if(digits is <1 or >9)
		{
			throw new InvalidDataException("Nieprawidłowy nagłówek bloku SCPI.");
		}
		byte[] lengthBytes=new byte[digits];
		stream.ReadExactly(lengthBytes);
		if(!int.TryParse(Encoding.ASCII.GetString(lengthBytes),out int length) ||
			length<0 ||
			length>maximumResponseBytes)
		{
			throw new InvalidDataException("Nieprawidłowa długość bloku SCPI.");
		}
		byte[] payload=new byte[length];
		stream.ReadExactly(payload);
		return payload;
	}
}
