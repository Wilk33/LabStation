use crate::access::{AccessPolicy,Role};
use crate::protocol::Command;
use crate::transport::{PortIo,Reply,Transport,TransportError};
use serde::{Deserialize,Serialize};

#[derive(Clone,Debug,Default,Deserialize,Serialize)]
pub struct Snapshot
{
	pub sequence:u64,
	pub requested_voltage_hundredths:Option<i32>,
	pub requested_current_thousandths:Option<i32>,
	pub set_voltage_hundredths:Option<i32>,
	pub set_current_thousandths:Option<i32>,
	pub measured_voltage_hundredths:Option<i32>,
	pub measured_current_thousandths:Option<i32>,
	pub requested_output_on:Option<bool>,
	pub output_on:Option<bool>,
	pub beep_on:Option<bool>,
	pub status_raw:Option<u8>,
	pub identity:Option<String>,
	pub error_code:Option<String>,
	pub error_detail:Option<String>,
}

pub struct Controller<P:PortIo>
{
	transport:Transport<P>,
	snapshot:Snapshot,
}

impl<P:PortIo> Controller<P>
{
	pub fn new(transport:Transport<P>)->Self
	{
		Self{transport,snapshot:Snapshot::default()}
	}

	pub fn snapshot(&self)->Snapshot
	{
		self.snapshot.clone()
	}

	pub fn execute(&mut self,policy:&AccessPolicy,command:Command)->Result<Reply,String>
	{
		if matches!(command,Command::Output(true))
		{
			policy.authorize(&command,true).map_err(|error|error.to_string())?;
			if policy.role == Role::Operator
			{
				self.execute(policy,Command::ReadSetVoltage)?;
				self.execute(policy,Command::ReadSetCurrent)?;
				if self.snapshot.set_voltage_hundredths.unwrap_or(i32::MAX)>policy.maximum_voltage_hundredths ||
					self.snapshot.set_current_thousandths.unwrap_or(i32::MAX)>policy.maximum_current_thousandths
				{
					return Err("Zastane nastawy przekraczają limity roli Operator".into());
				}
			}
		}
		if matches!(command,Command::Recall(_)|Command::Ocp(false)|Command::Ovp(false))
		{
			policy.authorize(&command,false).map_err(|error|error.to_string())?;
			self.execute(policy,Command::ReadStatus)?;
		}
		policy.authorize(&command,self.snapshot.output_on.unwrap_or(true))
			.map_err(|error|error.to_string())?;
		let reply=match self.transport.execute(&command)
		{
			Ok(reply)=>reply,
			Err(error)=>
			{
				self.snapshot.sequence+=1;
				self.snapshot.error_code=Some(error_code(&error).into());
				self.snapshot.error_detail=Some(error.to_string());
				self.snapshot.output_on=None;
				return Err(error.to_string());
			}
		};
		self.snapshot.sequence+=1;
		self.snapshot.error_code=None;
		self.snapshot.error_detail=None;
		match (&command,&reply)
		{
			(Command::SetVoltage(value),_)=>self.snapshot.requested_voltage_hundredths=Some(*value),
			(Command::SetCurrent(value),_)=>self.snapshot.requested_current_thousandths=Some(*value),
			(Command::Output(value),_)=>
			{
				self.snapshot.requested_output_on=Some(*value);
				self.snapshot.output_on=None;
			},
			(_,Reply::Voltage(value))=>self.snapshot.measured_voltage_hundredths=Some(*value),
			(_,Reply::Current(value))=>self.snapshot.measured_current_thousandths=Some(*value),
			(_,Reply::SetVoltage(value))=>self.snapshot.set_voltage_hundredths=Some(*value),
			(_,Reply::SetCurrent(value))=>self.snapshot.set_current_thousandths=Some(*value),
			(_,Reply::Status(flags))=>
			{
				self.snapshot.status_raw=Some(flags.raw);
				self.snapshot.output_on=Some(flags.output_on);
				self.snapshot.beep_on=Some(flags.beep_on);
			},
			(_,Reply::Identity(value))=>self.snapshot.identity=Some(value.clone()),
			_=>{},
		}
		if let Command::Output(expected)=command
		{
			self.execute(policy,Command::ReadStatus)?;
			if self.snapshot.output_on != Some(expected)
			{
				self.snapshot.error_code=Some("STATE".into());
				self.snapshot.error_detail=Some("Urządzenie nie potwierdziło zmiany stanu wyjścia".into());
				return Err(self.snapshot.error_detail.clone().unwrap_or_default());
			}
		}
		Ok(reply)
	}
}

fn error_code(error:&TransportError)->&'static str
{
	match error
	{
		TransportError::Io(inner) if inner.kind() == std::io::ErrorKind::TimedOut=>"TOUT",
		TransportError::Io(_)=>"PORT",
		TransportError::Protocol(_)=>"DATA",
	}
}
