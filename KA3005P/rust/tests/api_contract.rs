use korad_rs::api::ApiCommand;
use korad_rs::protocol::Command;

#[test]
fn api_maps_named_actions_to_typed_commands()
{
	let cases=[
		(r#"{"command":"set_voltage","value":1250}"#,"VSET1:12.50"),
		(r#"{"command":"set_current","value":1500}"#,"ISET1:1.500"),
		(r#"{"command":"output_on"}"#,"OUT1"),
		(r#"{"command":"output_off"}"#,"OUT0"),
		(r#"{"command":"beep_on"}"#,"BEEP1"),
		(r#"{"command":"ocp_off"}"#,"OCP0"),
		(r#"{"command":"ovp_on"}"#,"OVP1"),
		(r#"{"command":"save","bank":3}"#,"SAV3"),
		(r#"{"command":"recall","bank":2}"#,"RCL2"),
		(r#"{"command":"read_status"}"#,"STATUS?"),
		(r#"{"command":"identify"}"#,"*IDN?"),
	];
	for (json,expected) in cases
	{
		let input:ApiCommand=serde_json::from_str(json).unwrap();
		assert_eq!(input.to_command().unwrap().encode().unwrap(),expected.as_bytes());
	}
}

#[test]
fn api_rejects_missing_values_and_unknown_raw_commands()
{
	let missing:ApiCommand=serde_json::from_str(r#"{"command":"set_voltage"}"#).unwrap();
	assert!(missing.to_command().is_err());
	let raw:ApiCommand=serde_json::from_str(r#"{"command":"raw","value":1}"#).unwrap();
	assert!(raw.to_command().is_err());
	let _=Command::ReadStatus;
}
