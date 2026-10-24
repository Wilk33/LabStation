use korad_rs::access::{AccessPolicy,Role};
use korad_rs::control::Controller;
use korad_rs::protocol::Command;
use korad_rs::transport::{PortIo,Transport};
use std::collections::VecDeque;
use std::io::{self,Read,Write};
use std::sync::{Arc,Mutex};

struct FakePort
{
	input:VecDeque<u8>,
	writes:Arc<Mutex<Vec<String>>>,
}

impl Read for FakePort
{
	fn read(&mut self,buffer:&mut [u8])->io::Result<usize>
	{
		if let Some(value)=self.input.pop_front()
		{
			buffer[0]=value;
			Ok(1)
		}
		else
		{
			Err(io::Error::new(io::ErrorKind::TimedOut,"timeout"))
		}
	}
}

impl Write for FakePort
{
	fn write(&mut self,buffer:&[u8])->io::Result<usize>
	{
		self.writes.lock().unwrap().push(String::from_utf8(buffer.to_vec()).unwrap());
		Ok(buffer.len())
	}
	fn flush(&mut self)->io::Result<()>{Ok(())}
}

impl PortIo for FakePort
{
	fn clear_input(&mut self)->io::Result<()>{Ok(())}
}

#[test]
fn controller_rejects_unauthorized_write_before_touching_port()
{
	let port=FakePort{input:VecDeque::new(),writes:Arc::new(Mutex::new(Vec::new()))};
	let mut controller=Controller::new(Transport::new(port));
	let viewer=AccessPolicy::new(Role::Observer,3100,5100);
	assert!(controller.execute(&viewer,Command::Output(true)).is_err());
	assert_eq!(controller.snapshot().sequence,0);
}

#[test]
fn controller_marks_output_unknown_after_failed_status_and_preserves_error()
{
	let port=FakePort{input:VecDeque::new(),writes:Arc::new(Mutex::new(Vec::new()))};
	let mut controller=Controller::new(Transport::new(port));
	let expert=AccessPolicy::new(Role::Expert,3100,5100);
	assert!(controller.execute(&expert,Command::ReadStatus).is_err());
	let snapshot=controller.snapshot();
	assert_eq!(snapshot.output_on,None);
	assert_eq!(snapshot.error_code.as_deref(),Some("TOUT"));
}

#[test]
fn controller_updates_observed_measurement_only_after_complete_read()
{
	let port=FakePort{input:VecDeque::from(b"12.50".to_vec()),writes:Arc::new(Mutex::new(Vec::new()))};
	let mut controller=Controller::new(Transport::new(port));
	let viewer=AccessPolicy::new(Role::Observer,3100,5100);
	controller.execute(&viewer,Command::ReadVoltage).unwrap();
	assert_eq!(controller.snapshot().measured_voltage_hundredths,Some(1250));
}

#[test]
fn operator_cannot_enable_output_with_existing_settings_above_session_limits()
{
	let writes=Arc::new(Mutex::new(Vec::new()));
	let port=FakePort{input:VecDeque::from(b"20.001.500".to_vec()),writes:writes.clone()};
	let mut controller=Controller::new(Transport::new(port));
	let operator=AccessPolicy::new(Role::Operator,1250,1500);
	assert!(controller.execute(&operator,Command::Output(true)).is_err());
	assert!(!writes.lock().unwrap().iter().any(|value|value == "OUT1"));
}

#[test]
fn output_write_requires_fresh_status_confirmation()
{
	let writes=Arc::new(Mutex::new(Vec::new()));
	let port=FakePort{input:VecDeque::from(b"12.501.500\x40".to_vec()),writes:writes.clone()};
	let mut controller=Controller::new(Transport::new(port));
	let operator=AccessPolicy::new(Role::Operator,1250,1500);
	controller.execute(&operator,Command::Output(true)).unwrap();
	assert_eq!(controller.snapshot().output_on,Some(true));
	assert_eq!(&*writes.lock().unwrap(),&["VSET1?","ISET1?","OUT1","STATUS?"]);
}

#[test]
fn expert_recall_checks_fresh_status_before_writing()
{
	let writes=Arc::new(Mutex::new(Vec::new()));
	let port=FakePort{input:VecDeque::from(vec![0x40]),writes:writes.clone()};
	let mut controller=Controller::new(Transport::new(port));
	let expert=AccessPolicy::new(Role::Expert,3100,5100);
	assert!(controller.execute(&expert,Command::Recall(1)).is_err());
	assert_eq!(&*writes.lock().unwrap(),&["STATUS?"]);
}

#[test]
fn output_off_is_not_reported_as_success_when_status_stays_on()
{
	let writes=Arc::new(Mutex::new(Vec::new()));
	let port=FakePort{input:VecDeque::from(vec![0x40]),writes:writes.clone()};
	let mut controller=Controller::new(Transport::new(port));
	let expert=AccessPolicy::new(Role::Expert,3100,5100);
	assert!(controller.execute(&expert,Command::Output(false)).is_err());
	assert_eq!(controller.snapshot().output_on,Some(true));
	assert_eq!(controller.snapshot().error_code.as_deref(),Some("STATE"));
	assert_eq!(&*writes.lock().unwrap(),&["OUT0","STATUS?"]);
}

#[test]
fn any_io_failure_invalidates_previous_knowledge_of_output_state()
{
	let port=FakePort{input:VecDeque::from(vec![0x40]),writes:Arc::new(Mutex::new(Vec::new()))};
	let mut controller=Controller::new(Transport::new(port));
	let observer=AccessPolicy::new(Role::Observer,0,0);
	controller.execute(&observer,Command::ReadStatus).unwrap();
	assert_eq!(controller.snapshot().output_on,Some(true));
	assert!(controller.execute(&observer,Command::ReadVoltage).is_err());
	assert_eq!(controller.snapshot().output_on,None);
}
