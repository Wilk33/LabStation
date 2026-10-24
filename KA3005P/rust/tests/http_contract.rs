use korad_rs::hub::DeviceHub;
use korad_rs::server::ApiServer;
use std::io::{Read,Write};
use std::net::TcpStream;
use std::sync::Arc;
use std::time::{Duration,Instant};

fn request(port:u16,raw:&str)->String
{
	let mut stream=TcpStream::connect(("127.0.0.1",port)).unwrap();
	stream.write_all(raw.as_bytes()).unwrap();
	stream.shutdown(std::net::Shutdown::Write).unwrap();
	let mut answer=String::new();
	stream.read_to_string(&mut answer).unwrap();
	answer
}

#[test]
fn server_shutdown_waits_for_requests_already_accepted()
{
	let server=ApiServer::start(Arc::new(DeviceHub::new()),1250,1500).unwrap();
	let mut stream=TcpStream::connect(("127.0.0.1",server.port())).unwrap();
	stream.write_all(b"GET /v1/devices HTTP/1.1\r\nHost: localhost\r\nX-Incomplete:").unwrap();
	std::thread::sleep(Duration::from_millis(100));
	let start=Instant::now();
	drop(server);
	assert!(start.elapsed()>=Duration::from_millis(1200));
}

#[test]
fn trickling_request_cannot_delay_server_shutdown_indefinitely()
{
	let server=ApiServer::start(Arc::new(DeviceHub::new()),1250,1500).unwrap();
	let mut stream=TcpStream::connect(("127.0.0.1",server.port())).unwrap();
	stream.write_all(b"GET /v1/devices HTTP/1.1\r\nX-Slow: ").unwrap();
	std::thread::sleep(Duration::from_millis(100));
	let writer=std::thread::spawn(move||
	{
		for _ in 0..12
		{
			std::thread::sleep(Duration::from_millis(250));
			if stream.write_all(b"x").is_err(){break;}
		}
	});
	let start=Instant::now();
	drop(server);
	assert!(start.elapsed()<Duration::from_millis(2900));
	writer.join().unwrap();
}

#[test]
fn loopback_api_enforces_roles_before_device_access()
{
	let server=ApiServer::start(Arc::new(DeviceHub::new()),1250,1500).unwrap();
	let address=server.port();
	let denied=request(address,"GET /v1/devices HTTP/1.1\r\nHost: localhost\r\n\r\n");
	assert!(denied.starts_with("HTTP/1.1 401"));
	let viewer=server.viewer_token();
	let allowed=request(address,&format!("GET /v1/devices HTTP/1.1\r\nAuthorization: Bearer {viewer}\r\n\r\n"));
	assert!(allowed.starts_with("HTTP/1.1 200"));
	assert!(allowed.ends_with("[]"));
	let forbidden=request(address,&format!("POST /v1/connect HTTP/1.1\r\nAuthorization: Bearer {viewer}\r\nContent-Length: 15\r\n\r\n{{\"port\":\"COM3\"}}"));
	assert!(forbidden.starts_with("HTTP/1.1 403"));
}
