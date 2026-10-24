use crate::access::{AccessPolicy,Role};
use crate::api::ApiCommand;
use crate::dual::DualMode;
use crate::hub::DeviceHub;
use rand::RngCore;
use serde::Deserialize;
use serde_json::{Value,json};
use std::io::{self,Read,Write};
use std::net::{TcpListener,TcpStream};
use std::sync::atomic::{AtomicBool,Ordering};
use std::sync::{Arc,Mutex,RwLock};
use std::thread::{self,JoinHandle};
use std::time::{Duration,Instant};

const MAX_REQUEST:usize=16_384;

pub struct ApiServer
{
	port:u16,
	viewer_token:String,
	operator_token:String,
	expert_token:String,
	operator_limits:Arc<RwLock<(i32,i32)>>,
	stop:Arc<AtomicBool>,
	thread:Option<JoinHandle<()>>,
	workers:Arc<Mutex<Vec<JoinHandle<()>>>>,
}

#[derive(Deserialize)]
struct PortBody
{
	port:String,
}

#[derive(Deserialize)]
struct CommandBody
{
	port:String,
	#[serde(flatten)]
	command:ApiCommand,
}

#[derive(Deserialize)]
struct DualOutputBody
{
	first:String,
	second:String,
	enabled:bool,
}

#[derive(Deserialize)]
struct DualSetpointsBody
{
	first:String,
	second:String,
	mode:DualMode,
	voltage_hundredths:i32,
	current_thousandths:i32,
}

struct Request
{
	method:String,
	path:String,
	authorization:String,
	body:Vec<u8>,
}

impl ApiServer
{
	pub fn start(hub:Arc<DeviceHub>,maximum_voltage:i32,maximum_current:i32)->io::Result<Self>
	{
		let listener=TcpListener::bind(("127.0.0.1",0))?;
		listener.set_nonblocking(true)?;
		let port=listener.local_addr()?.port();
		let viewer_token=random_token();
		let operator_token=random_token();
		let expert_token=random_token();
		let operator_limits=Arc::new(RwLock::new((maximum_voltage.clamp(0,3100),maximum_current.clamp(0,5100))));
		let stop=Arc::new(AtomicBool::new(false));
		let server_tokens=[viewer_token.clone(),operator_token.clone(),expert_token.clone()];
		let worker_stop=stop.clone();
		let worker_limits=operator_limits.clone();
		let workers=Arc::new(Mutex::new(Vec::<JoinHandle<()>>::new()));
		let worker_handles=workers.clone();
		let thread=thread::spawn(move||
		{
			while !worker_stop.load(Ordering::Relaxed)
			{
				match listener.accept()
				{
					Ok((stream,_))=>
					{
						let hub=hub.clone();
						let tokens=server_tokens.clone();
						let limits=worker_limits.clone();
						let handle=thread::spawn(move||handle_connection(stream,&hub,&tokens,&limits));
						let mut handles=worker_handles.lock().unwrap();
						let mut index=0;
						while index<handles.len()
						{
							if handles[index].is_finished()
							{
								let finished=handles.swap_remove(index);
								let _=finished.join();
							}
							else{index+=1;}
						}
						handles.push(handle);
					},
					Err(error) if error.kind() == io::ErrorKind::WouldBlock=>
						thread::sleep(Duration::from_millis(20)),
					Err(_)=>break,
				}
			}
		});
		Ok(Self{port,viewer_token,operator_token,expert_token,operator_limits,stop,thread:Some(thread),workers})
	}

	pub fn port(&self)->u16{self.port}
	pub fn viewer_token(&self)->&str{&self.viewer_token}
	pub fn operator_token(&self)->&str{&self.operator_token}
	pub fn expert_token(&self)->&str{&self.expert_token}

	pub fn set_operator_limits(&self,maximum_voltage:i32,maximum_current:i32)
	{
		*self.operator_limits.write().unwrap()=(maximum_voltage.clamp(0,3100),maximum_current.clamp(0,5100));
	}
}

impl Drop for ApiServer
{
	fn drop(&mut self)
	{
		self.stop.store(true,Ordering::Relaxed);
		if let Some(thread)=self.thread.take()
		{
			let _=thread.join();
		}
		let workers=std::mem::take(&mut *self.workers.lock().unwrap());
		for worker in workers
		{
			let _=worker.join();
		}
	}
}

fn random_token()->String
{
	let mut bytes=[0u8;32];
	rand::rng().fill_bytes(&mut bytes);
	bytes.iter().map(|byte|format!("{byte:02x}")).collect()
}

fn same_token(a:&str,b:&str)->bool
{
	if a.len() != b.len()
	{
		return false;
	}
	a.bytes().zip(b.bytes()).fold(0u8,|difference,(left,right)|difference|(left^right)) == 0
}

fn handle_connection(mut stream:TcpStream,hub:&DeviceHub,tokens:&[String;3],limits:&RwLock<(i32,i32)>)
{
	if stream.set_nonblocking(false).is_err()
	{
		return;
	}
	let _=stream.set_write_timeout(Some(Duration::from_secs(2)));
	let (status,payload)=match read_request(&mut stream)
	{
		Ok(request)=>dispatch(request,hub,tokens,limits),
		Err(error)=>(400,json!({"error":error.to_string()})),
	};
	let body=serde_json::to_vec(&payload).unwrap_or_else(|_|b"{}".to_vec());
	let reason=match status{200=>"OK",400=>"Bad Request",401=>"Unauthorized",403=>"Forbidden",404=>"Not Found",_=>"Internal Server Error"};
	let header=format!("HTTP/1.1 {status} {reason}\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {}\r\nConnection: close\r\n\r\n",body.len());
	let _=stream.write_all(header.as_bytes());
	let _=stream.write_all(&body);
}

fn read_request(stream:&mut TcpStream)->io::Result<Request>
{
	let deadline=Instant::now()+Duration::from_secs(2);
	let mut bytes=Vec::new();
	let header_end=loop
	{
		if bytes.len()>MAX_REQUEST
		{
			return Err(io::Error::new(io::ErrorKind::InvalidData,"Za duże żądanie"));
		}
		if let Some(position)=bytes.windows(4).position(|item|item == b"\r\n\r\n")
		{
			break position+4;
		}
		let mut chunk=[0u8;1024];
		let count=read_before_deadline(stream,&mut chunk,deadline)?;
		if count == 0
		{
			return Err(io::Error::new(io::ErrorKind::UnexpectedEof,"Brak nagłówka HTTP"));
		}
		bytes.extend_from_slice(&chunk[..count]);
	};
	let header=std::str::from_utf8(&bytes[..header_end])
		.map_err(|_|io::Error::new(io::ErrorKind::InvalidData,"Nieprawidłowy nagłówek"))?;
	let mut lines=header.split("\r\n");
	let first=lines.next().unwrap_or_default();
	let mut first_parts=first.split_ascii_whitespace();
	let method=first_parts.next().unwrap_or_default().to_owned();
	let path=first_parts.next().unwrap_or_default().to_owned();
	let mut authorization=String::new();
	let mut length=0usize;
	for line in lines
	{
		if let Some((name,value))=line.split_once(':')
		{
			if name.eq_ignore_ascii_case("authorization")
			{
				authorization=value.trim().to_owned();
			}
			if name.eq_ignore_ascii_case("content-length")
			{
				length=value.trim().parse().map_err(|_|io::Error::new(io::ErrorKind::InvalidData,"Nieprawidłowa długość"))?;
			}
		}
	}
	if length>MAX_REQUEST || header_end+length>MAX_REQUEST
	{
		return Err(io::Error::new(io::ErrorKind::InvalidData,"Za duże żądanie"));
	}
	while bytes.len()<header_end+length
	{
		let mut chunk=[0u8;1024];
		let count=read_before_deadline(stream,&mut chunk,deadline)?;
		if count == 0
		{
			return Err(io::Error::new(io::ErrorKind::UnexpectedEof,"Niepełne żądanie"));
		}
		bytes.extend_from_slice(&chunk[..count]);
	}
	Ok(Request{method,path,authorization,body:bytes[header_end..header_end+length].to_vec()})
}

fn read_before_deadline(stream:&mut TcpStream,chunk:&mut [u8],deadline:Instant)->io::Result<usize>
{
	loop
	{
		let remaining=deadline.saturating_duration_since(Instant::now());
		if remaining.is_zero()
		{
			return Err(io::Error::new(io::ErrorKind::TimedOut,"Przekroczono czas żądania HTTP"));
		}
		stream.set_read_timeout(Some(remaining))?;
		match stream.read(chunk)
		{
			Err(error) if error.kind() == io::ErrorKind::Interrupted || error.kind() == io::ErrorKind::WouldBlock=>{},
			Err(error) if error.kind() == io::ErrorKind::TimedOut && Instant::now()<deadline=>{},
			value=>return value,
		}
	}
}

fn dispatch(request:Request,hub:&DeviceHub,tokens:&[String;3],limits:&RwLock<(i32,i32)>)->(u16,Value)
{
	let token=request.authorization.strip_prefix("Bearer ").unwrap_or_default();
	let role=if same_token(token,&tokens[0]){Some(Role::Observer)}
		else if same_token(token,&tokens[1]){Some(Role::Operator)}
		else if same_token(token,&tokens[2]){Some(Role::Expert)}
		else{None};
	let Some(role)=role else{return (401,json!({"error":"Brak poprawnego tokenu"}))};
	let limits=*limits.read().unwrap();
	let policy=match role
	{
		Role::Observer=>AccessPolicy::new(role,0,0),
		Role::Operator=>AccessPolicy::new(role,limits.0,limits.1),
		Role::Expert=>AccessPolicy::new(role,3100,5100),
	};
	if request.method == "GET" && request.path == "/v1/ports"
	{
		return result_json(DeviceHub::available_ports());
	}
	if request.method == "GET" && request.path == "/v1/devices"
	{
		return (200,json!(hub.connected_ports()));
	}
	if request.method == "GET" && request.path.starts_with("/v1/state?port=")
	{
		return result_json(hub.snapshot(&request.path[15..]));
	}
	if request.method == "POST" && request.path == "/v1/poll"
	{
		let body:PortBody=match serde_json::from_slice(&request.body){Ok(value)=>value,Err(error)=>return (400,json!({"error":error.to_string()}))};
		return result_json(hub.poll(&body.port));
	}
	if request.method == "POST" && (request.path == "/v1/connect" || request.path == "/v1/disconnect")
	{
		if role == Role::Observer{return (403,json!({"error":"Rola tylko do odczytu"}))};
		let body:PortBody=match serde_json::from_slice(&request.body){Ok(value)=>value,Err(error)=>return (400,json!({"error":error.to_string()}))};
		if request.path == "/v1/connect"
		{
			return result_json(hub.connect(&body.port));
		}
		return result_json(hub.disconnect(&body.port));
	}
	if request.method == "POST" && request.path == "/v1/command"
	{
		let body:CommandBody=match serde_json::from_slice(&request.body){Ok(value)=>value,Err(error)=>return (400,json!({"error":error.to_string()}))};
		let command=match body.command.to_command(){Ok(value)=>value,Err(error)=>return (400,json!({"error":error}))};
		return result_json(hub.execute(&body.port,&policy,command));
	}
	if request.method == "POST" && request.path == "/v1/dual/output"
	{
		if role == Role::Observer{return (403,json!({"error":"Rola tylko do odczytu"}))};
		let body:DualOutputBody=match serde_json::from_slice(&request.body){Ok(value)=>value,Err(error)=>return (400,json!({"error":error.to_string()}))};
		return result_json(hub.dual_output(&body.first,&body.second,&policy,body.enabled));
	}
	if request.method == "POST" && request.path == "/v1/dual/setpoints"
	{
		if role == Role::Observer{return (403,json!({"error":"Rola tylko do odczytu"}))};
		let body:DualSetpointsBody=match serde_json::from_slice(&request.body){Ok(value)=>value,Err(error)=>return (400,json!({"error":error.to_string()}))};
		return result_json(hub.dual_setpoints(&body.first,&body.second,body.mode,body.voltage_hundredths,body.current_thousandths,&policy));
	}
	(404,json!({"error":"Nieznany adres"}))
}

fn result_json<T:serde::Serialize>(value:Result<T,String>)->(u16,Value)
{
	match value
	{
		Ok(value)=>(200,json!(value)),
		Err(error)=>(400,json!({"error":error})),
	}
}
