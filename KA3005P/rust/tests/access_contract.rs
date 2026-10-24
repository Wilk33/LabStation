use korad_rs::access::{AccessPolicy,Role};
use korad_rs::protocol::Command;

#[test]
fn observer_reads_but_cannot_change_device()
{
	let policy=AccessPolicy::new(Role::Observer,1250,1500);
	assert!(policy.authorize(&Command::ReadStatus,false).is_ok());
	assert!(policy.authorize(&Command::SetVoltage(500),false).is_err());
	assert!(policy.authorize(&Command::Output(true),false).is_err());
}

#[test]
fn operator_obeys_session_limits_and_cannot_change_extended_features()
{
	let policy=AccessPolicy::new(Role::Operator,1250,1500);
	assert!(policy.authorize(&Command::SetVoltage(1250),false).is_ok());
	assert!(policy.authorize(&Command::SetVoltage(1251),false).is_err());
	assert!(policy.authorize(&Command::SetCurrent(1501),false).is_err());
	assert!(policy.authorize(&Command::Output(true),false).is_ok());
	assert!(policy.authorize(&Command::Ovp(false),false).is_err());
}

#[test]
fn expert_has_complete_command_set_but_recall_and_protection_disable_require_off()
{
	let policy=AccessPolicy::new(Role::Expert,3100,5100);
	assert!(policy.authorize(&Command::SetVoltage(3100),false).is_ok());
	assert!(policy.authorize(&Command::Save(5),true).is_ok());
	assert!(policy.authorize(&Command::Recall(1),true).is_err());
	assert!(policy.authorize(&Command::Recall(1),false).is_ok());
	assert!(policy.authorize(&Command::Ocp(false),true).is_err());
	assert!(policy.authorize(&Command::Ocp(false),false).is_ok());
	assert!(policy.authorize(&Command::Beep(true),true).is_ok());
}
