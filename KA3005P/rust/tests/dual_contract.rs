use korad_rs::dual::{DualMode,physical_setpoints,run_dual_output};
use korad_rs::protocol::Command;

#[test]
fn series_parallel_and_symmetric_split_match_existing_controller()
{
	let series=physical_setpoints(DualMode::Series,2501,1500).unwrap();
	assert_eq!(series.voltage,[1250,1251]);
	assert_eq!(series.current,[1500,1500]);
	let parallel=physical_setpoints(DualMode::Parallel,1200,1501).unwrap();
	assert_eq!(parallel.voltage,[1200,1200]);
	assert_eq!(parallel.current,[750,751]);
	let symmetric=physical_setpoints(DualMode::Symmetric,1200,1500).unwrap();
	assert_eq!(symmetric.voltage,[1200,1200]);
	assert_eq!(symmetric.current,[1500,1500]);
	assert!(physical_setpoints(DualMode::Series,6201,1500).is_err());
	assert!(physical_setpoints(DualMode::Parallel,1200,10201).is_err());
}

#[test]
fn dual_output_failure_attempts_off_on_both_channels()
{
	let mut calls=Vec::new();
	let result=run_dual_output(true,|index,command|
	{
		calls.push((index,command.clone()));
		if index == 1 && matches!(command,Command::Output(true))
		{
			return Err("PORT".into());
		}
		Ok(())
	});
	assert!(result.is_err());
	assert_eq!(calls.len(),4);
	assert!(matches!(calls[2],(0,Command::Output(false))));
	assert!(matches!(calls[3],(1,Command::Output(false))));
}
