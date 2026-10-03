using System.Text;

namespace LabStation.Instruments.Transport;

public sealed class Vxi11Transport : IInstrumentTransport,IRawInstrumentTransport,ILocalControlTransport
{
	private const uint Program=395183;
	private readonly object gate=new();
	private readonly RpcConnection core;
	private readonly Vxi11Options options;
	private readonly uint link;
	private readonly int maximumWrite;
	private bool disposed;

	public Vxi11Transport(string host,int mapperPort=111)
		: this(host,new Vxi11Options(),mapperPort)
	{
	}

	public Vxi11Transport(string host,Vxi11Options options,int mapperPort=111)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(host);
		ArgumentNullException.ThrowIfNull(options);
		if(options.MaximumResponseBytes<1024)
		{
			throw new ArgumentOutOfRangeException(nameof(options));
		}
		this.options=options;
		using(RpcConnection mapper=new(
			host,
			mapperPort,
			options.ConnectTimeout,
			options.IoTimeout,
			options.MaximumResponseBytes))
		{
			Xdr port=mapper.Call(100000,2,3,xdr=>
			{
				xdr.Put(Program);
				xdr.Put(1);
				xdr.Put(6);
				xdr.Put(0);
			});
			uint number=port.Get();
			if(number == 0 || number>65535)
			{
				throw new IOException("VXI-11 jest niedostępne pod tym adresem.");
			}
			core=new RpcConnection(
				host,
				(int)number,
				options.ConnectTimeout,
				options.IoTimeout,
				options.MaximumResponseBytes);
		}
		try
		{
			Xdr response=core.Call(Program,1,10,xdr=>
			{
				xdr.Put((uint)Random.Shared.Next(1,int.MaxValue));
				xdr.Put(0);
				xdr.Put(ToMilliseconds(options.IoTimeout));
				xdr.PutBytes(Encoding.ASCII.GetBytes(options.DeviceName));
			});
			Check(response.Get());
			link=response.Get();
			response.Get();
			uint size=response.Get();
			maximumWrite=(int)Math.Clamp(size,1u,1048576u);
		}
		catch
		{
			core.Dispose();
			throw;
		}
	}

	public void Write(string command)
	{
		lock(gate)
		{
			WriteCore(Encoding.ASCII.GetBytes(command+options.CommandTerminator));
		}
	}

	public void Write(ReadOnlyMemory<byte> data)
	{
		lock(gate)
		{
			WriteCore(data.ToArray());
		}
	}

	public byte[] Query(string command)
	{
		lock(gate)
		{
			WriteCore(Encoding.ASCII.GetBytes(command+options.CommandTerminator));
			using MemoryStream output=new();
			while(true)
			{
				Xdr reply=core.Call(Program,1,12,xdr=>
				{
					xdr.Put(link);
					xdr.Put(1048576);
					xdr.Put(ToMilliseconds(options.IoTimeout));
					xdr.Put(ToMilliseconds(options.IoTimeout));
					xdr.Put(0);
					xdr.Put(0);
				});
				Check(reply.Get());
				uint reason=reply.Get();
				byte[] data=reply.GetBytes();
				if(data.Length == 0 && (reason&4) == 0)
				{
					throw new IOException("Pusta odpowiedź VXI-11.");
				}
				if(output.Length+data.Length>options.MaximumResponseBytes)
				{
					throw new IOException("Przekroczony limit danych.");
				}
				output.Write(data);
				if((reason&4) != 0)
				{
					return output.ToArray();
				}
				if((reason&2) != 0)
				{
					throw new IOException("Nieoczekiwane zakończenie transferu znakiem końca.");
				}
			}
		}
	}

	public void ReturnToLocal()
	{
		lock(gate)
		{
			ObjectDisposedException.ThrowIf(disposed,this);
			ReturnToLocalCore();
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
			try
			{
				ReturnToLocalCore();
			}
			catch(Exception)
			{
			}
			try
			{
				core.Call(Program,1,23,xdr=>xdr.Put(link));
			}
			catch(Exception)
			{
			}
			core.Dispose();
		}
	}

	private void ReturnToLocalCore()
	{
		Xdr reply=core.Call(Program,1,17,xdr=>
		{
			xdr.Put(link);
			xdr.Put(0);
			xdr.Put(ToMilliseconds(options.IoTimeout));
			xdr.Put(ToMilliseconds(options.IoTimeout));
		});
		Check(reply.Get());
	}

	private void WriteCore(byte[] data)
	{
		ObjectDisposedException.ThrowIf(disposed,this);
		for(int offset=0;offset<data.Length;)
		{
			int count=Math.Min(maximumWrite,data.Length-offset);
			byte[] chunk=data.AsSpan(offset,count).ToArray();
			uint flags=offset+count == data.Length ? 8u : 0u;
			Xdr reply=core.Call(Program,1,11,xdr=>
			{
				xdr.Put(link);
				xdr.Put(ToMilliseconds(options.IoTimeout));
				xdr.Put(ToMilliseconds(options.IoTimeout));
				xdr.Put(flags);
				xdr.PutBytes(chunk);
			});
			Check(reply.Get());
			if(reply.Get() != count)
			{
				throw new IOException("Niepełny zapis VXI-11.");
			}
			offset+=count;
		}
	}

	private static uint ToMilliseconds(TimeSpan value)=>(uint)Math.Clamp(value.TotalMilliseconds,1,uint.MaxValue);

	private static void Check(uint error)
	{
		if(error != 0)
		{
			throw new IOException(
				$"Błąd VXI-11: {error}"+(error == 15 ? " - przekroczony czas odpowiedzi." : "."));
		}
	}
}
