use korad_rs::control::Snapshot;
use korad_rs::gui_model::{AppMode,GuiModel};

fn sample(output_on:bool,voltage:i32,current:i32)->Snapshot
{
	Snapshot
	{
		measured_voltage_hundredths:Some(voltage),
		measured_current_thousandths:Some(current),
		output_on:Some(output_on),
		..Snapshot::default()
	}
}

#[test]
fn starts_as_fixed_single_supply_application()
{
	let model=GuiModel::default();
	assert_eq!(model.mode,AppMode::Single);
	assert_eq!(model.window_size(),[300.0,360.0]);
	assert_eq!(model.status_text(),"Status: OFFLINE");
}

#[test]
fn dual_mode_uses_its_fixed_window_size()
{
	let model=GuiModel{mode:AppMode::Dual,..GuiModel::default()};
	assert_eq!(model.window_size(),[380.0,460.0]);
}

#[test]
fn measurement_time_advances_only_while_output_is_on_and_keeps_sixty_seconds()
{
	let mut model=GuiModel::default();
	model.accept_snapshot("COM3",sample(false,100,20),1.0);
	assert_eq!(model.measurement_seconds,0.0);
	model.accept_snapshot("COM3",sample(true,101,21),0.5);
	assert_eq!(model.measurement_seconds,0.5);
	for index in 0..130
	{
		model.accept_snapshot("COM3",sample(true,102,index),0.5);
	}
	assert!(model.history.front().unwrap().time_seconds>=model.measurement_seconds-60.0);
	assert!(model.history.back().unwrap().time_seconds<=model.measurement_seconds);
}

#[test]
fn status_uses_short_error_code_and_detail_remains_separate()
{
	let mut model=GuiModel::default();
	let mut snapshot=sample(false,0,0);
	snapshot.error_code=Some("TOUT".into());
	snapshot.error_detail=Some("Pełny opis błędu komunikacji".into());
	model.accept_snapshot("COM3",snapshot,0.5);
	assert_eq!(model.status_text(),"Status: TOUT");
	assert_eq!(model.error_detail(),Some("Pełny opis błędu komunikacji"));
}
