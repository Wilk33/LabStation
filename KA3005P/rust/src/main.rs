#![cfg_attr(not(debug_assertions),windows_subsystem="windows")]

use eframe::egui;
use korad_rs::gui::KoradApp;

fn main()->eframe::Result
{
	let icon=load_icon();
	let size=[300.0,360.0];
	let options=eframe::NativeOptions
	{
		viewport:egui::ViewportBuilder::default()
			.with_title("Korad KA3005P v0.3.0")
			.with_inner_size(size)
			.with_min_inner_size(size)
			.with_max_inner_size(size)
			.with_resizable(false)
			.with_maximize_button(false)
			.with_icon(icon),
		centered:true,
		..Default::default()
	};
	eframe::run_native("Korad KA3005P",options,Box::new(|context|Ok(Box::new(KoradApp::new(&context.egui_ctx)))))
}

fn load_icon()->egui::IconData
{
	let bytes=include_bytes!("../../assets/icons/KoradS-256.png");
	match image::load_from_memory(bytes)
	{
		Ok(image)=>
		{
			let rgba=image.into_rgba8();
			let (width,height)=rgba.dimensions();
			egui::IconData{rgba:rgba.into_raw(),width,height}
		},
		Err(_)=>egui::IconData::default(),
	}
}
