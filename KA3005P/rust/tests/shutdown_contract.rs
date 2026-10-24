use korad_rs::access::{AccessPolicy,Role};
use korad_rs::hub::DeviceHub;
use korad_rs::protocol::Command;

#[test]
fn shutdown_rejects_new_commands_before_touching_device()
{
	let hub=DeviceHub::new();
	hub.begin_shutdown();
	assert!(hub.connect("COM3").unwrap_err().contains("zamykan"));
	let observer=AccessPolicy::new(Role::Observer,0,0);
	assert!(hub.execute("COM3",&observer,Command::ReadStatus).unwrap_err().contains("zamykan"));
	assert!(hub.poll("COM3").unwrap_err().contains("zamykan"));
}
