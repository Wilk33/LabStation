use crate::protocol::{Command,ProtocolError};
use serde::{Deserialize,Serialize};

#[derive(Clone,Copy,Debug,Deserialize,Serialize,PartialEq,Eq)]
#[serde(rename_all="snake_case")]
pub enum Role
{
	Observer,
	Operator,
	Expert,
}

#[derive(Clone,Copy,Debug,Deserialize,Serialize)]
pub struct AccessPolicy
{
	pub role:Role,
	pub maximum_voltage_hundredths:i32,
	pub maximum_current_thousandths:i32,
}

impl AccessPolicy
{
	pub fn new(role:Role,maximum_voltage_hundredths:i32,maximum_current_thousandths:i32)->Self
	{
		Self
		{
			role,
			maximum_voltage_hundredths:maximum_voltage_hundredths.clamp(0,3100),
			maximum_current_thousandths:maximum_current_thousandths.clamp(0,5100),
		}
	}

	pub fn authorize(&self,command:&Command,output_on:bool)->Result<(),ProtocolError>
	{
		command.encode()?;
		if command.is_query()
		{
			return Ok(());
		}
		if self.role == Role::Observer
		{
			return Err(ProtocolError("Rola nie ma prawa zapisu".into()));
		}
		match command
		{
			Command::SetVoltage(value) if *value > self.maximum_voltage_hundredths=>
				Err(ProtocolError("Przekroczono limit napięcia sesji".into())),
			Command::SetCurrent(value) if *value > self.maximum_current_thousandths=>
				Err(ProtocolError("Przekroczono limit prądu sesji".into())),
			Command::SetVoltage(_) | Command::SetCurrent(_) | Command::Output(_)=>Ok(()),
			_ if self.role != Role::Expert=>
				Err(ProtocolError("Polecenie wymaga roli eksperta".into())),
			Command::Recall(_) | Command::Ocp(false) | Command::Ovp(false) if output_on=>
				Err(ProtocolError("Polecenie wymaga wyłączonego wyjścia".into())),
			_=>Ok(()),
		}
	}
}
