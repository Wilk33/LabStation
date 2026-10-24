use crate::protocol::Command;
use serde::{Deserialize,Serialize};

#[derive(Clone,Debug,Deserialize,Serialize)]
pub struct ApiCommand
{
	pub command:String,
	pub value:Option<i32>,
	pub bank:Option<u8>,
}

impl ApiCommand
{
	pub fn to_command(&self)->Result<Command,String>
	{
		let value=||self.value.ok_or_else(||"Brak pola value".to_owned());
		let bank=||self.bank.ok_or_else(||"Brak pola bank".to_owned());
		let command=match self.command.as_str()
		{
			"set_voltage"=>Command::SetVoltage(value()?),
			"set_current"=>Command::SetCurrent(value()?),
			"output_on"=>Command::Output(true),
			"output_off"=>Command::Output(false),
			"beep_on"=>Command::Beep(true),
			"beep_off"=>Command::Beep(false),
			"ocp_on"=>Command::Ocp(true),
			"ocp_off"=>Command::Ocp(false),
			"ovp_on"=>Command::Ovp(true),
			"ovp_off"=>Command::Ovp(false),
			"save"=>Command::Save(bank()?),
			"recall"=>Command::Recall(bank()?),
			"read_voltage"=>Command::ReadVoltage,
			"read_current"=>Command::ReadCurrent,
			"read_set_voltage"=>Command::ReadSetVoltage,
			"read_set_current"=>Command::ReadSetCurrent,
			"read_status"=>Command::ReadStatus,
			"identify"=>Command::Identify,
			_=>return Err("Nieznane polecenie".into()),
		};
		command.encode().map_err(|error|error.to_string())?;
		Ok(command)
	}
}
