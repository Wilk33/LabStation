use korad_rs::hub::DeviceHub;
use korad_rs::access::{AccessPolicy,Role};
use korad_rs::protocol::Command;
use korad_rs::server::ApiServer;
use std::io::{self,BufRead};
use std::sync::Arc;

fn main()
{
	let args:Vec<String>=std::env::args().skip(1).collect();
	if args.len() == 1 && args[0] == "ports"
	{
		match DeviceHub::available_ports()
		{
			Ok(ports)=>
			{
				for port in ports
				{
					println!("{port}");
				}
			},
			Err(error)=>fail(&error),
		}
		return;
	}
	if args.len() == 2 && args[0] == "probe"
	{
		let hub=DeviceHub::new();
		if let Err(error)=hub.connect(&args[1]){fail(&error);}
		let observer=AccessPolicy::new(Role::Observer,0,0);
		for command in [Command::Identify,Command::ReadSetVoltage,Command::ReadSetCurrent,Command::ReadVoltage,Command::ReadCurrent,Command::ReadStatus]
		{
			if let Err(error)=hub.execute(&args[1],&observer,command)
			{
				eprintln!("Odczyt: {error}");
			}
		}
		let snapshot=hub.snapshot(&args[1]).unwrap_or_else(|error|fail(&error));
		println!("{}",serde_json::to_string_pretty(&snapshot).unwrap_or_default());
		return;
	}
	if args.len() != 1 || args[0] != "serve"
	{
		println!("Użycie: korad-rs ports | probe COMx | serve");
		return;
	}
	let hub=Arc::new(DeviceHub::new());
	let server=match ApiServer::start(hub.clone(),1250,1500)
	{
		Ok(server)=>server,
		Err(error)=>fail(&format!("Nie udało się uruchomić API: {error}")),
	};
	println!("API: http://127.0.0.1:{}",server.port());
	println!("Observer: {}",server.viewer_token());
	println!("Operator: {}",server.operator_token());
	println!("Expert: {}",server.expert_token());
	println!("Operator limit: 12,50 V / 1,500 A");
	println!("Wpisz quit, aby zamknąć serwer i połączone sesje.");
	for line in io::stdin().lock().lines()
	{
		match line
		{
			Ok(value) if value.trim().eq_ignore_ascii_case("quit")=>break,
			Ok(_)=>{},
			Err(_)=>break,
		}
	}
	hub.begin_shutdown();
	let mut unsafe_ports=Vec::new();
	for port in hub.connected_ports()
	{
		let mut error=None;
		for _ in 0..3
		{
			match hub.disconnect(&port)
			{
				Ok(())=>{error=None;break;},
				Err(value)=>error=Some(value),
			}
		}
		if let Some(error)=error
		{
			eprintln!("Nie potwierdzono OFF na porcie {port}: {error}");
			unsafe_ports.push(port);
		}
	}
	drop(server);
	if !unsafe_ports.is_empty()
	{
		fail(&format!("Stan wyjść nieznany na portach: {}",unsafe_ports.join(", ")));
	}
}

fn fail(message:&str)->!
{
	eprintln!("{message}");
	std::process::exit(1)
}
