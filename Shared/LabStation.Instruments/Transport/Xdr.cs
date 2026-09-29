using System.Buffers.Binary;

namespace LabStation.Instruments.Transport;

internal sealed class Xdr
{
	private readonly MemoryStream stream;
	private readonly int maximumBlockSize;

	public Xdr(int maximumBlockSize)
	{
		stream=new();
		this.maximumBlockSize=maximumBlockSize;
	}

	public Xdr(byte[] bytes,int maximumBlockSize)
	{
		stream=new(bytes,false);
		this.maximumBlockSize=maximumBlockSize;
	}

	public void Put(uint value)
	{
		Span<byte> bytes=stackalloc byte[4];
		BinaryPrimitives.WriteUInt32BigEndian(bytes,value);
		stream.Write(bytes);
	}

	public uint Get()
	{
		Span<byte> bytes=stackalloc byte[4];
		stream.ReadExactly(bytes);
		return BinaryPrimitives.ReadUInt32BigEndian(bytes);
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

	public byte[] GetBytes()
	{
		uint size=Get();
		if(size>maximumBlockSize)
		{
			throw new InvalidDataException("Zbyt duży blok RPC.");
		}
		byte[] bytes=new byte[size];
		stream.ReadExactly(bytes);
		int padding=(4-(int)size%4)%4;
		Span<byte> pad=stackalloc byte[3];
		stream.ReadExactly(pad[..padding]);
		return bytes;
	}

	public byte[] Bytes()=>stream.ToArray();
}
