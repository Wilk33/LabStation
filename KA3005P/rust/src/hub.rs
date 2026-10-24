use crate::access::AccessPolicy;
use crate::control::{Controller,Snapshot};
use crate::dual::{DualMode,physical_setpoints,run_dual_output};
use crate::protocol::Command;
use crate::transport::{Reply,SerialAdapter,Transport};
use std::collections::HashMap;
use std::sync::atomic::{AtomicBool,Ordering};
use std::sync::{Arc,Mutex};

type Session=Arc<Mutex<Controller<SerialAdapter>>>;

#[derive(Default)]
pub struct DeviceHub
{
	sessions:Mutex<HashMap<String,Session>>,
	control_gate:Mutex<()>,
	shutting_down:AtomicBool,
}

impl DeviceHub
{
	pub fn new()->Self
	{
		Self::default()
	}

	pub fn available_ports()->Result<Vec<String>,String>
	{
		serialport::available_ports()
			.map(|ports|ports.into_iter().map(|port|port.port_name).collect())
			.map_err(|error|error.to_string())
	}

	pub fn connected_ports(&self)->Vec<String>
	{
		self.sessions.lock().unwrap().keys().cloned().collect()
	}

	pub fn begin_shutdown(&self)
	{
		let _operation=self.control_gate.lock().unwrap();
		self.shutting_down.store(true,Ordering::Release);
	}

	fn ensure_running(&self)->Result<(),String>
	{
		if self.shutting_down.load(Ordering::Acquire)
		{
			return Err("Aplikacja jest zamykana".into());
		}
		Ok(())
	}

	pub fn connect(&self,port:&str)->Result<Snapshot,String>
	{
		let _operation=self.control_gate.lock().unwrap();
		self.ensure_running()?;
		let key=normalize_port(port)?;
		let mut sessions=self.sessions.lock().unwrap();
		if sessions.contains_key(&key)
		{
			return Err("Port jest już połączony".into());
		}
		let adapter=SerialAdapter::open(&key).map_err(|error|error.to_string())?;
		let mut controller=Controller::new(Transport::new(adapter));
		let expert=AccessPolicy::new(crate::access::Role::Expert,3100,5100);
		controller.execute(&expert,Command::ReadStatus)?;
		let snapshot=controller.snapshot();
		sessions.insert(key,Arc::new(Mutex::new(controller)));
		Ok(snapshot)
	}

	pub fn snapshot(&self,port:&str)->Result<Snapshot,String>
	{
		let session=self.get(port)?;
		let snapshot=session.lock().unwrap().snapshot();
		Ok(snapshot)
	}

	pub fn execute(&self,port:&str,policy:&AccessPolicy,command:Command)->Result<(Reply,Snapshot),String>
	{
		let _operation=self.control_gate.lock().unwrap();
		self.ensure_running()?;
		self.execute_inner(port,policy,command)
	}

	fn execute_inner(&self,port:&str,policy:&AccessPolicy,command:Command)->Result<(Reply,Snapshot),String>
	{
		let session=self.get(port)?;
		let mut controller=session.lock().unwrap();
		let reply=controller.execute(policy,command)?;
		Ok((reply,controller.snapshot()))
	}

	pub fn poll(&self,port:&str)->Result<Snapshot,String>
	{
		let _operation=self.control_gate.lock().unwrap();
		self.ensure_running()?;
		let policy=AccessPolicy::new(crate::access::Role::Observer,3100,5100);
		for command in [Command::ReadVoltage,Command::ReadCurrent,Command::ReadStatus]
		{
			self.execute_inner(port,&policy,command)?;
		}
		self.snapshot(port)
	}

	pub fn disconnect(&self,port:&str)->Result<(),String>
	{
		let _operation=self.control_gate.lock().unwrap();
		let key=normalize_port(port)?;
		let session=self.get(&key)?;
		let mut controller=session.lock().unwrap();
		let expert=AccessPolicy::new(crate::access::Role::Expert,3100,5100);
		controller.execute(&expert,Command::Output(false))?;
		drop(controller);
		self.sessions.lock().unwrap().remove(&key);
		Ok(())
	}

	pub fn dual_output(&self,first:&str,second:&str,policy:&AccessPolicy,enabled:bool)->Result<[Snapshot;2],String>
	{
		let _operation=self.control_gate.lock().unwrap();
		self.ensure_running()?;
		validate_pair(first,second)?;
		self.get(first)?;
		self.get(second)?;
		policy.authorize(&Command::Output(enabled),true).map_err(|error|error.to_string())?;
		if enabled && policy.role == crate::access::Role::Operator
		{
			for port in [first,second]
			{
				let (voltage,_)=self.execute_inner(port,policy,Command::ReadSetVoltage)?;
				let (current,_)=self.execute_inner(port,policy,Command::ReadSetCurrent)?;
				if !matches!(voltage,Reply::SetVoltage(value) if value<=policy.maximum_voltage_hundredths) ||
					!matches!(current,Reply::SetCurrent(value) if value<=policy.maximum_current_thousandths)
				{
					return Err("Zastane nastawy Dual przekraczają limity roli Operator".into());
				}
			}
		}
		run_dual_output(enabled,|index,command|
		{
			let port=if index == 0{first}else{second};
			self.execute_inner(port,policy,command).map(|_|())
		})?;
		Ok([self.snapshot(first)?,self.snapshot(second)?])
	}

	pub fn dual_setpoints(&self,first:&str,second:&str,mode:DualMode,voltage:i32,current:i32,policy:&AccessPolicy)->Result<[Snapshot;2],String>
	{
		let _operation=self.control_gate.lock().unwrap();
		self.ensure_running()?;
		validate_pair(first,second)?;
		self.get(first)?;
		self.get(second)?;
		let physical=physical_setpoints(mode,voltage,current)?;
		for index in 0..2
		{
			policy.authorize(&Command::SetVoltage(physical.voltage[index]),true).map_err(|error|error.to_string())?;
			policy.authorize(&Command::SetCurrent(physical.current[index]),true).map_err(|error|error.to_string())?;
		}
		for index in 0..2
		{
			let port=if index == 0{first}else{second};
			for command in [Command::SetVoltage(physical.voltage[index]),Command::SetCurrent(physical.current[index])]
			{
				if let Err(error)=self.execute_inner(port,policy,command)
				{
					let safety=run_dual_output(false,|position,off|
					{
						self.execute_inner(if position == 0{first}else{second},policy,off).map(|_|())
					});
					return Err(format!("{error}; próba wyłączenia obu wyjść: {safety:?}"));
				}
			}
		}
		Ok([self.snapshot(first)?,self.snapshot(second)?])
	}

	fn get(&self,port:&str)->Result<Session,String>
	{
		let key=normalize_port(port)?;
		self.sessions.lock().unwrap().get(&key).cloned()
			.ok_or_else(||"Port nie jest połączony".into())
	}
}

fn validate_pair(first:&str,second:&str)->Result<(),String>
{
	if normalize_port(first)? == normalize_port(second)?
	{
		return Err("Tryb Dual wymaga dwóch różnych portów COM".into());
	}
	Ok(())
}

fn normalize_port(port:&str)->Result<String,String>
{
	let key=port.trim().to_ascii_uppercase();
	if key.len()>32 || !key.starts_with("COM") ||
		!key[3..].bytes().all(|byte|byte.is_ascii_digit()) || key.len()<=3
	{
		return Err("Niedozwolona nazwa portu COM".into());
	}
	Ok(key)
}
