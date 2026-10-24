use korad_rs::protocol::Command;
use korad_rs::transport::{PortIo,Reply,Transport};
use std::collections::VecDeque;
use std::io::{self,Read,Write};
use std::sync::{Arc,Mutex};
use std::time::{Duration,Instant};

struct FakePort
{
	input:VecDeque<u8>,
	writes:Arc<Mutex<Vec<String>>>,
	clears:Arc<Mutex<usize>>,
}

impl Read for FakePort
{
	fn read(&mut self,buffer:&mut [u8])->io::Result<usize>
	{
		if self.input.is_empty()
		{
			return Err(io::Error::new(io::ErrorKind::TimedOut,"timeout"));
		}
		buffer[0]=self.input.pop_front().unwrap();
		Ok(1)
	}
}

impl Write for FakePort
{
	fn write(&mut self,buffer:&[u8])->io::Result<usize>
	{
		self.writes.lock().unwrap().push(String::from_utf8(buffer.to_vec()).unwrap());
		Ok(buffer.len())
	}

	fn flush(&mut self)->io::Result<()>
	{
		Ok(())
	}
}

impl PortIo for FakePort
{
	fn clear_input(&mut self)->io::Result<()>
	{
		*self.clears.lock().unwrap()+=1;
		Ok(())
	}
}

#[test]
fn query_reads_complete_reply_and_write_never_waits_for_acknowledgment()
{
	let writes=Arc::new(Mutex::new(Vec::new()));
	let clears=Arc::new(Mutex::new(0));
	let port=FakePort
	{
		input:VecDeque::from(b"12.50".to_vec()),
		writes:writes.clone(),
		clears:clears.clone(),
	};
	let mut transport=Transport::new(port);
	assert!(matches!(transport.execute(&Command::ReadVoltage).unwrap(),Reply::Voltage(1250)));
	assert!(matches!(transport.execute(&Command::Output(false)).unwrap(),Reply::Written));
	assert_eq!(*writes.lock().unwrap(),vec!["VOUT1?","OUT0"]);
	assert_eq!(*clears.lock().unwrap(),2);
}

#[test]
fn partial_measurement_is_error_and_never_used_as_a_value()
{
	let port=FakePort
	{
		input:VecDeque::from(b"12.".to_vec()),
		writes:Arc::new(Mutex::new(Vec::new())),
		clears:Arc::new(Mutex::new(0)),
	};
	let mut transport=Transport::new(port);
	assert!(transport.execute(&Command::ReadVoltage).is_err());
}

struct SlowPort
{
	input:VecDeque<u8>,
}

impl Read for SlowPort
{
	fn read(&mut self,buffer:&mut [u8])->io::Result<usize>
	{
		std::thread::sleep(Duration::from_millis(350));
		if let Some(value)=self.input.pop_front()
		{
			buffer[0]=value;
			Ok(1)
		}
		else{Err(io::Error::new(io::ErrorKind::TimedOut,"timeout"))}
	}
}

impl Write for SlowPort
{
	fn write(&mut self,buffer:&[u8])->io::Result<usize>{Ok(buffer.len())}
	fn flush(&mut self)->io::Result<()>{Ok(())}
}

impl PortIo for SlowPort
{
	fn clear_input(&mut self)->io::Result<()>{Ok(())}
}

#[test]
fn a_trickling_reply_cannot_extend_one_query_without_bound()
{
	let mut transport=Transport::new(SlowPort{input:VecDeque::from(b"12.50".to_vec())});
	let start=Instant::now();
	assert!(transport.execute(&Command::ReadVoltage).is_err());
	assert!(start.elapsed()<Duration::from_millis(2200));
}

struct SlowWritePort;

impl Read for SlowWritePort
{
	fn read(&mut self,_buffer:&mut [u8])->io::Result<usize>{Ok(0)}
}

impl Write for SlowWritePort
{
	fn write(&mut self,buffer:&[u8])->io::Result<usize>
	{
		std::thread::sleep(Duration::from_millis(1600));
		Ok(buffer.len())
	}
	fn flush(&mut self)->io::Result<()>{Ok(())}
}

impl PortIo for SlowWritePort
{
	fn clear_input(&mut self)->io::Result<()>{Ok(())}
}

#[test]
fn slow_write_is_included_in_command_deadline()
{
	let mut transport=Transport::new(SlowWritePort);
	assert!(transport.execute(&Command::Output(false)).is_err());
}
