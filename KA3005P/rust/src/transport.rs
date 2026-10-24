use crate::protocol::{Command,ProtocolError,Response,StatusFlags};
use serde::{Deserialize,Serialize};
use std::fmt;
use std::io::{self,Read,Write};
use std::thread;
use std::time::{Duration,Instant};

const COMMAND_INTERVAL:Duration=Duration::from_millis(45);
const IO_TIMEOUT:Duration=Duration::from_millis(500);
const COMMAND_TIMEOUT:Duration=Duration::from_millis(1500);

pub trait PortIo:Read+Write+Send
{
	fn clear_input(&mut self)->io::Result<()>;
	fn set_timeout(&mut self,_timeout:Duration)->io::Result<()>{Ok(())}
}

pub struct SerialAdapter
{
	port:Box<dyn serialport::SerialPort>,
}

impl SerialAdapter
{
	pub fn open(name:&str)->io::Result<Self>
	{
		let port=serialport::new(name,9600)
			.data_bits(serialport::DataBits::Eight)
			.parity(serialport::Parity::None)
			.stop_bits(serialport::StopBits::One)
			.flow_control(serialport::FlowControl::None)
			.dtr_on_open(false)
			.timeout(IO_TIMEOUT)
			.open()
			.map_err(io::Error::other)?;
		Ok(Self{port})
	}
}

impl Read for SerialAdapter
{
	fn read(&mut self,buffer:&mut [u8])->io::Result<usize>
	{
		self.port.read(buffer)
	}
}

impl Write for SerialAdapter
{
	fn write(&mut self,buffer:&[u8])->io::Result<usize>
	{
		self.port.write(buffer)
	}

	fn flush(&mut self)->io::Result<()>
	{
		self.port.flush()
	}
}

impl PortIo for SerialAdapter
{
	fn clear_input(&mut self)->io::Result<()>
	{
		self.port.clear(serialport::ClearBuffer::Input).map_err(io::Error::other)
	}

	fn set_timeout(&mut self,timeout:Duration)->io::Result<()>
	{
		self.port.set_timeout(timeout).map_err(io::Error::other)
	}
}

#[derive(Clone,Debug,Deserialize,Serialize)]
#[serde(tag="type",content="value",rename_all="snake_case")]
pub enum Reply
{
	Written,
	Voltage(i32),
	Current(i32),
	SetVoltage(i32),
	SetCurrent(i32),
	Status(StatusFlags),
	Identity(String),
}

#[derive(Debug)]
pub enum TransportError
{
	Io(io::Error),
	Protocol(ProtocolError),
}

impl fmt::Display for TransportError
{
	fn fmt(&self,f:&mut fmt::Formatter<'_>)->fmt::Result
	{
		match self
		{
			Self::Io(error)=>write!(f,"PORT: {error}"),
			Self::Protocol(error)=>write!(f,"DATA: {error}"),
		}
	}
}

impl std::error::Error for TransportError {}

impl From<io::Error> for TransportError
{
	fn from(error:io::Error)->Self
	{
		Self::Io(error)
	}
}

impl From<ProtocolError> for TransportError
{
	fn from(error:ProtocolError)->Self
	{
		Self::Protocol(error)
	}
}

pub struct Transport<P:PortIo>
{
	port:P,
	last_write:Option<Instant>,
}

impl<P:PortIo> Transport<P>
{
	pub fn new(port:P)->Self
	{
		Self{port,last_write:None}
	}

	pub fn execute(&mut self,command:&Command)->Result<Reply,TransportError>
	{
		let bytes=command.encode()?;
		if let Some(last_write)=self.last_write
		{
			let elapsed=last_write.elapsed();
			if elapsed < COMMAND_INTERVAL
			{
				thread::sleep(COMMAND_INTERVAL-elapsed);
			}
		}
		let deadline=Instant::now()+COMMAND_TIMEOUT;
		self.port.clear_input()?;
		self.last_write=Some(Instant::now());
		self.port.write_all(&bytes)?;
		if Instant::now()>=deadline
		{
			return Err(io::Error::new(io::ErrorKind::TimedOut,"Przekroczono czas całego polecenia").into());
		}
		match command
		{
			Command::ReadVoltage=>Ok(Reply::Voltage(Response::voltage(&self.read_five(deadline)?)?)),
			Command::ReadCurrent=>Ok(Reply::Current(Response::current(&self.read_five(deadline)?)?)),
			Command::ReadSetVoltage=>Ok(Reply::SetVoltage(Response::voltage(&self.read_five(deadline)?)?)),
			Command::ReadSetCurrent=>Ok(Reply::SetCurrent(Response::current(&self.read_five(deadline)?)?)),
			Command::ReadStatus=>
			{
				Ok(Reply::Status(StatusFlags::from_byte(self.read_one(deadline)?)))
			},
			Command::Identify=>
			{
				let mut bytes=Vec::new();
				while bytes.len()<128
				{
					match self.read_one(deadline)
					{
						Ok(0)|Ok(b'\n')=>break,
						Ok(next)=>bytes.push(next),
						Err(error) if error.kind() == io::ErrorKind::TimedOut && !bytes.is_empty()=>break,
						Err(error)=>return Err(error.into()),
					}
				}
				let text=String::from_utf8_lossy(&bytes).trim_matches(char::from(0)).trim().to_owned();
				if text.is_empty()
				{
					return Err(TransportError::Protocol(ProtocolError("Pusta identyfikacja".into())));
				}
				Ok(Reply::Identity(text))
			},
			_=>Ok(Reply::Written),
		}
	}

	fn read_five(&mut self,deadline:Instant)->io::Result<[u8;5]>
	{
		let mut bytes=[0;5];
		for byte in &mut bytes
		{
			*byte=self.read_one(deadline)?;
		}
		Ok(bytes)
	}

	fn read_one(&mut self,deadline:Instant)->io::Result<u8>
	{
		loop
		{
			let remaining=deadline.saturating_duration_since(Instant::now());
			if remaining.is_zero()
			{
				return Err(io::Error::new(io::ErrorKind::TimedOut,"Przekroczono czas całego polecenia"));
			}
			self.port.set_timeout(remaining.min(IO_TIMEOUT))?;
			let mut byte=[0];
			match self.port.read(&mut byte)
			{
				Ok(1) if Instant::now()<=deadline=>return Ok(byte[0]),
				Ok(1)=>return Err(io::Error::new(io::ErrorKind::TimedOut,"Przekroczono czas całego polecenia")),
				Ok(0)=>{},
				Ok(_)=>return Err(io::Error::new(io::ErrorKind::InvalidData,"Niespodziewana długość odczytu")),
				Err(error) if error.kind() == io::ErrorKind::Interrupted=>{},
				Err(error)=>return Err(error),
			}
		}
	}
}
