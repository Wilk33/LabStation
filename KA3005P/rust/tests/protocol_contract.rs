use korad_rs::protocol::{Command, Response, StatusFlags};

#[test]
fn encodes_all_single_channel_commands_without_terminator()
{
	let cases=[
		(Command::SetVoltage(1250),"VSET1:12.50"),
		(Command::SetCurrent(1500),"ISET1:1.500"),
		(Command::Output(true),"OUT1"),
		(Command::Output(false),"OUT0"),
		(Command::Beep(true),"BEEP1"),
		(Command::Ocp(true),"OCP1"),
		(Command::Ovp(false),"OVP0"),
		(Command::Save(5),"SAV5"),
		(Command::Recall(1),"RCL1"),
		(Command::ReadVoltage,"VOUT1?"),
		(Command::ReadCurrent,"IOUT1?"),
		(Command::ReadSetVoltage,"VSET1?"),
		(Command::ReadSetCurrent,"ISET1?"),
		(Command::ReadStatus,"STATUS?"),
		(Command::Identify,"*IDN?"),
	];
	for (command,expected) in cases
	{
		assert_eq!(command.encode().unwrap(),expected.as_bytes());
	}
}

#[test]
fn refuses_out_of_range_values_and_memory_slots()
{
	assert!(Command::SetVoltage(3101).encode().is_err());
	assert!(Command::SetCurrent(5101).encode().is_err());
	assert!(Command::Save(0).encode().is_err());
	assert!(Command::Recall(6).encode().is_err());
}

#[test]
fn parses_fixed_width_measurements_and_binary_status()
{
	assert_eq!(Response::voltage(b"12.50").unwrap(),1250);
	assert_eq!(Response::current(b"1.500").unwrap(),1500);
	assert!(Response::voltage(b"12.5").is_err());
	let flags=StatusFlags::from_byte(0b0101_0001);
	assert!(flags.output_on);
	assert!(flags.beep_on);
	assert!(flags.channel_one_cv);
}
