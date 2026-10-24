use crate::protocol::Command;
use serde::{Deserialize,Serialize};

#[derive(Clone,Copy,Debug,Deserialize,Serialize,PartialEq,Eq)]
#[serde(rename_all="snake_case")]
pub enum DualMode
{
	Series,
	Parallel,
	Symmetric,
}

#[derive(Clone,Copy,Debug,Deserialize,Serialize,PartialEq,Eq)]
pub struct PhysicalSetpoints
{
	pub voltage:[i32;2],
	pub current:[i32;2],
}

pub fn physical_setpoints(mode:DualMode,voltage:i32,current:i32)->Result<PhysicalSetpoints,String>
{
	let maximum_voltage=if mode == DualMode::Series{6200}else{3100};
	let maximum_current=if mode == DualMode::Parallel{10200}else{5100};
	if !(0..=maximum_voltage).contains(&voltage) || !(0..=maximum_current).contains(&current)
	{
		return Err("Nastawa poza zakresem trybu Dual".into());
	}
	let voltage=if mode == DualMode::Series{[voltage/2,voltage-voltage/2]}else{[voltage,voltage]};
	let current=if mode == DualMode::Parallel{[current/2,current-current/2]}else{[current,current]};
	Ok(PhysicalSetpoints{voltage,current})
}

pub fn run_dual_output<F>(enabled:bool,mut execute:F)->Result<(),String>
where F:FnMut(usize,Command)->Result<(),String>
{
	let first=execute(0,Command::Output(enabled));
	let second=execute(1,Command::Output(enabled));
	if first.is_ok() && second.is_ok()
	{
		return Ok(());
	}
	let mut errors=Vec::new();
	if let Err(error)=first{errors.push(format!("P1: {error}"));}
	if let Err(error)=second{errors.push(format!("P2: {error}"));}
	if enabled
	{
		if let Err(error)=execute(0,Command::Output(false)){errors.push(format!("P1 OFF: {error}"));}
		if let Err(error)=execute(1,Command::Output(false)){errors.push(format!("P2 OFF: {error}"));}
	}
	Err(errors.join("; "))
}
