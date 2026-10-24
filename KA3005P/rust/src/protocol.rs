use serde::{Deserialize,Serialize};
use std::fmt;

#[derive(Clone,Debug,Deserialize,Serialize)]
#[serde(tag="type",rename_all="snake_case")]
pub enum Command
{
	SetVoltage(i32),
	SetCurrent(i32),
	Output(bool),
	Beep(bool),
	Ocp(bool),
	Ovp(bool),
	Save(u8),
	Recall(u8),
	ReadVoltage,
	ReadCurrent,
	ReadSetVoltage,
	ReadSetCurrent,
	ReadStatus,
	Identify,
}

#[derive(Debug,PartialEq,Eq)]
pub struct ProtocolError(pub String);

impl fmt::Display for ProtocolError
{
	fn fmt(&self,f:&mut fmt::Formatter<'_>)->fmt::Result
	{
		f.write_str(&self.0)
	}
}

impl std::error::Error for ProtocolError {}

impl Command
{
	pub fn encode(&self)->Result<Vec<u8>,ProtocolError>
	{
		let value=match self
		{
			Self::SetVoltage(hundredths) if (0..=3100).contains(hundredths)=>
				format!("VSET1:{:02}.{:02}",hundredths/100,hundredths%100),
			Self::SetCurrent(thousandths) if (0..=5100).contains(thousandths)=>
				format!("ISET1:{}.{}",thousandths/1000,format_args!("{:03}",thousandths%1000)),
			Self::SetVoltage(_) | Self::SetCurrent(_)=>
				return Err(ProtocolError("Nastawa poza zakresem KA3005P".into())),
			Self::Output(on)=>format!("OUT{}",u8::from(*on)),
			Self::Beep(on)=>format!("BEEP{}",u8::from(*on)),
			Self::Ocp(on)=>format!("OCP{}",u8::from(*on)),
			Self::Ovp(on)=>format!("OVP{}",u8::from(*on)),
			Self::Save(bank) if (1..=5).contains(bank)=>format!("SAV{bank}"),
			Self::Recall(bank) if (1..=5).contains(bank)=>format!("RCL{bank}"),
			Self::Save(_) | Self::Recall(_)=>
				return Err(ProtocolError("Numer pamięci poza zakresem 1-5".into())),
			Self::ReadVoltage=>"VOUT1?".into(),
			Self::ReadCurrent=>"IOUT1?".into(),
			Self::ReadSetVoltage=>"VSET1?".into(),
			Self::ReadSetCurrent=>"ISET1?".into(),
			Self::ReadStatus=>"STATUS?".into(),
			Self::Identify=>"*IDN?".into(),
		};
		Ok(value.into_bytes())
	}

	pub fn is_query(&self)->bool
	{
		matches!(self,Self::ReadVoltage|Self::ReadCurrent|Self::ReadSetVoltage|Self::ReadSetCurrent|Self::ReadStatus|Self::Identify)
	}
}

pub struct Response;

impl Response
{
	pub fn voltage(reply:&[u8])->Result<i32,ProtocolError>
	{
		Self::decimal(reply,2,3100)
	}

	pub fn current(reply:&[u8])->Result<i32,ProtocolError>
	{
		Self::decimal(reply,1,5100)
	}

	fn decimal(reply:&[u8],dot:usize,maximum:i32)->Result<i32,ProtocolError>
	{
		if reply.len() != 5 || reply[dot] != b'.' ||
			reply.iter().enumerate().any(|(i,byte)| i != dot && !byte.is_ascii_digit())
		{
			return Err(ProtocolError("Nieprawidłowa odpowiedź liczbowa".into()));
		}
		let mut value=0;
		for (i,byte) in reply.iter().enumerate()
		{
			if i != dot
			{
				value=value*10+i32::from(byte-b'0');
			}
		}
		if value > maximum
		{
			return Err(ProtocolError("Odpowiedź poza zakresem".into()));
		}
		Ok(value)
	}
}

#[derive(Clone,Copy,Debug,Deserialize,Serialize)]
pub struct StatusFlags
{
	pub raw:u8,
	pub channel_one_cv:bool,
	pub channel_two_cv:bool,
	pub beep_on:bool,
	pub output_on:bool,
}

impl StatusFlags
{
	pub fn from_byte(raw:u8)->Self
	{
		Self
		{
			raw,
			channel_one_cv:raw&0x01 != 0,
			channel_two_cv:raw&0x02 != 0,
			beep_on:raw&0x10 != 0,
			output_on:raw&0x40 != 0,
		}
	}
}
