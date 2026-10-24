use crate::access::{AccessPolicy,Role};
use crate::control::Snapshot;
use crate::dual::DualMode;
use crate::gui_model::{AppMode,GuiModel};
use crate::hub::DeviceHub;
use crate::protocol::Command;
use crate::server::ApiServer;
use eframe::egui::{self,Color32,RichText,Stroke,Vec2};
use egui_plot::{Line,Plot,PlotPoints,VLine};
use std::io::Write;
use std::sync::{Arc,mpsc};
use std::thread::{self,JoinHandle};
use std::time::{Duration,Instant};

const BACKGROUND:Color32=Color32::from_rgb(112,112,112);
const CONTROL:Color32=Color32::from_rgb(148,148,148);
const DARK:Color32=Color32::from_rgb(40,40,40);
const GREEN:Color32=Color32::from_rgb(0,255,35);
const RED:Color32=Color32::from_rgb(255,35,35);
const ORANGE:Color32=Color32::from_rgb(255,125,0);
const APP_VERSION:&str="0.3.0";

enum WorkerRequest
{
	RefreshPorts,
	Connect(Vec<String>),
	Disconnect(Vec<String>),
	SetSingle(String,i32,i32),
	Execute(String,Command),
	DualOutput(String,String,bool),
	DualSetpoints(String,String,DualMode,i32,i32),
	Poll(Vec<String>),
	Stop,
}

enum WorkerResponse
{
	Ports(Result<Vec<String>,String>),
	Connected(Result<Vec<(String,Snapshot)>,String>),
	Disconnected(Result<Vec<String>,String>),
	Updated(Result<Vec<(String,Snapshot)>,String>),
}

struct ChartState
{
	show_voltage:bool,
	show_current:bool,
	selected_cursor:Option<usize>,
	cursors:[Option<f64>;2],
}

impl Default for ChartState
{
	fn default()->Self
	{
		Self{show_voltage:true,show_current:true,selected_cursor:None,cursors:[None,None]}
	}
}

pub struct KoradApp
{
	model:GuiModel,
	hub:Arc<DeviceHub>,
	server:Option<ApiServer>,
	request_tx:mpsc::Sender<WorkerRequest>,
	response_rx:mpsc::Receiver<WorkerResponse>,
	worker:Option<JoinHandle<()>>,
	voltage_text:String,
	current_text:String,
	step_milli:bool,
	busy:bool,
	last_poll:Instant,
	last_sample:Instant,
	show_chart:bool,
	show_about:bool,
	show_license:bool,
	show_ai:bool,
	chart:ChartState,
	last_error_code:Option<String>,
	last_error_detail:Option<String>,
}

impl KoradApp
{
	pub fn new(context:&egui::Context)->Self
	{
		configure_style(context);
		let hub=Arc::new(DeviceHub::new());
		let server=ApiServer::start(hub.clone(),1250,1500).ok();
		let (request_tx,request_rx)=mpsc::channel();
		let (response_tx,response_rx)=mpsc::channel();
		let worker_hub=hub.clone();
		let repaint=context.clone();
		let worker=thread::spawn(move||worker_loop(worker_hub,request_rx,response_tx,repaint));
		let app=Self
		{
			model:GuiModel::default(),hub,server,request_tx,response_rx,worker:Some(worker),
			voltage_text:"12,00".into(),current_text:"1,000".into(),step_milli:true,
			busy:false,last_poll:Instant::now(),last_sample:Instant::now(),show_chart:false,
			show_about:false,show_license:false,show_ai:false,chart:ChartState::default(),
			last_error_code:None,last_error_detail:None,
		};
		let _=app.request_tx.send(WorkerRequest::RefreshPorts);
		app
	}

	fn process_responses(&mut self)
	{
		while let Ok(response)=self.response_rx.try_recv()
		{
			self.busy=false;
			match response
			{
				WorkerResponse::Ports(result)=>match result
				{
					Ok(ports)=>self.model.set_ports(ports),
					Err(error)=>self.set_error(&error),
				},
				WorkerResponse::Connected(result)=>match result
				{
					Ok(items)=>
					{
						self.clear_error();
						for (port,snapshot) in items{self.model.mark_connected(&port,snapshot);}
						self.sync_editors_from_snapshot();
					},
					Err(error)=>self.set_error(&error),
				},
				WorkerResponse::Disconnected(result)=>match result
				{
					Ok(ports)=>
					{
						self.clear_error();
						for port in ports{self.model.mark_disconnected(&port);}
					},
					Err(error)=>self.set_error(&error),
				},
				WorkerResponse::Updated(result)=>match result
				{
					Ok(items)=>
					{
						self.clear_error();
						let elapsed=self.last_sample.elapsed().as_secs_f64();
						self.last_sample=Instant::now();
						let primary=self.model.selected_first.clone();
						for (port,snapshot) in items.iter().filter(|(port,_)|*port != primary)
						{
							self.model.accept_snapshot(port,snapshot.clone(),0.0);
						}
						for (port,snapshot) in items.iter().filter(|(port,_)|*port == primary)
						{
							self.model.accept_snapshot(port,snapshot.clone(),elapsed);
						}
					},
					Err(error)=>self.set_error(&error),
				},
			}
		}
	}

	fn set_error(&mut self,error:&str)
	{
		self.last_error_code=Some(short_error_code(error).into());
		self.last_error_detail=Some(error.into());
	}

	fn clear_error(&mut self)
	{
		self.last_error_code=None;
		self.last_error_detail=None;
	}

	fn status_text(&self)->String
	{
		if let Some(code)=&self.last_error_code{format!("Status: {code}")}else{self.model.status_text()}
	}

	fn status_detail(&self)->Option<&str>
	{
		self.last_error_detail.as_deref().or_else(||self.model.error_detail())
	}

	fn primary_snapshot(&self)->Snapshot
	{
		self.model.snapshots.get(&self.model.selected_first).cloned().unwrap_or_default()
	}

	fn secondary_snapshot(&self)->Snapshot
	{
		self.model.snapshots.get(&self.model.selected_second).cloned().unwrap_or_default()
	}

	fn sync_editors_from_snapshot(&mut self)
	{
		let first=self.primary_snapshot();
		let second=self.secondary_snapshot();
		let voltage=match self.model.mode
		{
			AppMode::Single=>first.set_voltage_hundredths,
			AppMode::Dual=>match self.model.dual_mode
			{
				DualMode::Series=>combine(first.set_voltage_hundredths,second.set_voltage_hundredths,|a,b|a+b),
				_=>first.set_voltage_hundredths,
			},
		};
		let current=match self.model.mode
		{
			AppMode::Single=>first.set_current_thousandths,
			AppMode::Dual=>match self.model.dual_mode
			{
				DualMode::Parallel=>combine(first.set_current_thousandths,second.set_current_thousandths,|a,b|a+b),
				_=>first.set_current_thousandths,
			},
		};
		if let Some(value)=voltage{self.voltage_text=format_voltage(value);}
		if let Some(value)=current{self.current_text=format_current(value);}
	}

	fn connected_for_mode(&self)->bool
	{
		match self.model.mode
		{
			AppMode::Single=>self.model.is_connected(&self.model.selected_first),
			AppMode::Dual=>self.model.is_connected(&self.model.selected_first) && self.model.is_connected(&self.model.selected_second),
		}
	}

	fn selected_ports(&self)->Vec<String>
	{
		match self.model.mode
		{
			AppMode::Single=>vec![self.model.selected_first.clone()],
			AppMode::Dual=>vec![self.model.selected_first.clone(),self.model.selected_second.clone()],
		}
	}

	fn send(&mut self,request:WorkerRequest)
	{
		if self.busy{return;}
		self.busy=self.request_tx.send(request).is_ok();
	}

	fn toggle_connection(&mut self)
	{
		let ports=self.selected_ports();
		if ports.iter().any(|port|port.is_empty())
		{
			self.set_error("Wybierz port COM");
			return;
		}
		if ports.len() == 2 && ports[0] == ports[1]
		{
			self.set_error("Tryb Dual wymaga dwóch różnych portów COM");
			return;
		}
		if self.connected_for_mode(){self.send(WorkerRequest::Disconnect(ports));}
		else{self.send(WorkerRequest::Connect(ports));}
	}

	fn apply_setpoints(&mut self)
	{
		let Some(voltage)=parse_scaled(&self.voltage_text,100) else
		{
			self.set_error("Nieprawidłowa nastawa napięcia");
			return;
		};
		let Some(current)=parse_scaled(&self.current_text,1000) else
		{
			self.set_error("Nieprawidłowa nastawa prądu");
			return;
		};
		match self.model.mode
		{
			AppMode::Single=>self.send(WorkerRequest::SetSingle(self.model.selected_first.clone(),voltage,current)),
			AppMode::Dual=>self.send(WorkerRequest::DualSetpoints(
				self.model.selected_first.clone(),self.model.selected_second.clone(),self.model.dual_mode,voltage,current)),
		}
	}

	fn toggle_output(&mut self)
	{
		let enabled=self.primary_snapshot().output_on != Some(true);
		match self.model.mode
		{
			AppMode::Single=>self.send(WorkerRequest::Execute(self.model.selected_first.clone(),Command::Output(enabled))),
			AppMode::Dual=>self.send(WorkerRequest::DualOutput(
				self.model.selected_first.clone(),self.model.selected_second.clone(),enabled)),
		}
	}

	fn switch_mode(&mut self,mode:AppMode,context:&egui::Context)
	{
		if self.model.mode == mode{return;}
		let connected:Vec<String>=self.model.connected.iter().cloned().collect();
		if !connected.is_empty(){self.send(WorkerRequest::Disconnect(connected));}
		self.model.mode=mode;
		let size=self.model.window_size();
		context.send_viewport_cmd(egui::ViewportCommand::InnerSize(Vec2::new(size[0],size[1])));
		context.send_viewport_cmd(egui::ViewportCommand::MinInnerSize(Vec2::new(size[0],size[1])));
		context.send_viewport_cmd(egui::ViewportCommand::MaxInnerSize(Vec2::new(size[0],size[1])));
		self.sync_editors_from_snapshot();
	}

	fn periodic_poll(&mut self,context:&egui::Context)
	{
		if self.last_poll.elapsed()>=Duration::from_millis(500) && !self.busy
		{
			self.last_poll=Instant::now();
			let ports=self.selected_ports();
			if ports.iter().any(|port|self.model.is_connected(port)){self.send(WorkerRequest::Poll(ports));}
		}
		context.request_repaint_after(Duration::from_millis(100));
	}

	fn menu_bar(&mut self,ui:&mut egui::Ui)
	{
		egui::MenuBar::new().ui(ui,|ui|
		{
			ui.menu_button("Tryb pracy",|ui|
			{
				if ui.selectable_label(self.model.mode == AppMode::Single,"Pojedynczy").clicked()
				{
					self.switch_mode(AppMode::Single,ui.ctx());
					ui.close();
				}
				if ui.selectable_label(self.model.mode == AppMode::Dual,"Dual").clicked()
				{
					self.switch_mode(AppMode::Dual,ui.ctx());
					ui.close();
				}
			});
			if ui.button("Wykres").clicked(){self.show_chart=true;}
			ui.menu_button("Zapisz jako",|ui|
			{
				if ui.button("Napięcie CSV").clicked(){self.export_csv(ExportKind::Voltage);ui.close();}
				if ui.button("Prąd CSV").clicked(){self.export_csv(ExportKind::Current);ui.close();}
				if ui.button("Napięcie i prąd CSV").clicked(){self.export_csv(ExportKind::Both);ui.close();}
			});
			ui.menu_button("AI",|ui|
			{
				if ui.button("Dostęp lokalny").clicked(){self.show_ai=true;ui.close();}
			});
			ui.menu_button("O aplikacji",|ui|
			{
				if ui.button("Autor").clicked(){self.show_about=true;ui.close();}
				if ui.button("Licencja").clicked(){self.show_license=true;ui.close();}
			});
		});
	}

	fn single_panel(&mut self,ui:&mut egui::Ui)
	{
		ui.add_space(8.0);
		ui.horizontal(|ui|
		{
			let enabled=!self.connected_for_mode();
			ui.label(RichText::new("Port:").size(17.0));
			port_combo(ui,"single_port",&self.model.ports,&mut self.model.selected_first,enabled);
			ui.with_layout(egui::Layout::right_to_left(egui::Align::Center),|ui|
			{
				let label=if self.connected_for_mode(){"Offline"}else{"Online"};
				if standard_button(ui,label,72.0).clicked(){self.toggle_connection();}
			});
		});
		ui.add_space(8.0);
		self.numeric_editor(ui,NumericKind::Voltage);
		ui.add_space(5.0);
		self.numeric_editor(ui,NumericKind::Current);
		ui.add_space(5.0);
		ui.horizontal(|ui|
		{
			ui.add_space((ui.available_width()-146.0).max(0.0));
			self.multiplier_buttons(ui);
			let on=self.primary_snapshot().output_on == Some(true);
			if standard_button(ui,if on{"OFF"}else{"ON"},72.0).clicked(){self.toggle_output();}
		});
		ui.add_space(8.0);
		let snapshot=self.primary_snapshot();
		ui.horizontal(|ui|
		{
			ui.label(RichText::new(resistance_text(&snapshot)).monospace().size(17.0));
			ui.with_layout(egui::Layout::right_to_left(egui::Align::TOP),|ui|
			{
				ui.vertical(|ui|
				{
					ui.label(value_text(format_voltage(snapshot.measured_voltage_hundredths.unwrap_or(0))," V",GREEN));
					ui.label(value_text(format_current(snapshot.measured_current_thousandths.unwrap_or(0))," A",RED));
				});
			});
		});
		ui.with_layout(egui::Layout::bottom_up(egui::Align::LEFT),|ui|self.status(ui));
	}

	fn dual_panel(&mut self,ui:&mut egui::Ui)
	{
		ui.add_space(4.0);
		ui.horizontal(|ui|
		{
			let enabled=!self.connected_for_mode();
			ui.label("Port 1:");
			port_combo(ui,"dual_first",&self.model.ports,&mut self.model.selected_first,enabled);
			ui.with_layout(egui::Layout::right_to_left(egui::Align::Center),|ui|
			{
				let label=if self.connected_for_mode(){"Offline"}else{"Online"};
				if standard_button(ui,label,86.0).clicked(){self.toggle_connection();}
			});
		});
		ui.horizontal(|ui|
		{
			let enabled=!self.connected_for_mode();
			ui.label("Port 2:");
			port_combo(ui,"dual_second",&self.model.ports,&mut self.model.selected_second,enabled);
			egui::ComboBox::from_id_salt("dual_mode").selected_text(dual_mode_name(self.model.dual_mode)).show_ui(ui,|ui|
			{
				ui.selectable_value(&mut self.model.dual_mode,DualMode::Series,"Szeregowy");
				ui.selectable_value(&mut self.model.dual_mode,DualMode::Parallel,"Równoległy");
				ui.selectable_value(&mut self.model.dual_mode,DualMode::Symmetric,"Symetryczny");
			});
		});
		ui.add_space(4.0);
		self.numeric_editor(ui,NumericKind::Voltage);
		self.numeric_editor(ui,NumericKind::Current);
		ui.horizontal(|ui|
		{
			ui.add_space((ui.available_width()-160.0).max(0.0));
			self.multiplier_buttons(ui);
			let on=self.primary_snapshot().output_on == Some(true);
			if standard_button(ui,if on{"OFF"}else{"ON"},86.0).clicked(){self.toggle_output();}
		});
		ui.add_space(5.0);
		let first=self.primary_snapshot();
		let second=self.secondary_snapshot();
		ui.columns(2,|columns|
		{
			physical_panel(&mut columns[0],"Zasilacz 1",&first);
			physical_panel(&mut columns[1],"Zasilacz 2",&second);
		});
		ui.add_space(5.0);
		if self.model.dual_mode == DualMode::Symmetric
		{
			ui.columns(2,|columns|
			{
				measurement_block(&mut columns[0],"P1",&first,false);
				measurement_block(&mut columns[1],"P2",&second,true);
			});
		}
		else
		{
			let voltage=aggregate_voltage(self.model.dual_mode,&first,&second);
			let current=aggregate_current(self.model.dual_mode,&first,&second);
			ui.with_layout(egui::Layout::right_to_left(egui::Align::TOP),|ui|
			{
				ui.vertical(|ui|
				{
					ui.label(value_text(format_voltage(voltage)," V",GREEN));
					ui.label(value_text(format_current(current)," A",RED));
				});
			});
		}
		ui.with_layout(egui::Layout::bottom_up(egui::Align::LEFT),|ui|self.status(ui));
	}

	fn numeric_editor(&mut self,ui:&mut egui::Ui,kind:NumericKind)
	{
		let (text,unit,label)=match kind
		{
			NumericKind::Voltage=>(&mut self.voltage_text,"V",maximum_voltage_description(self.model.mode,self.model.dual_mode)),
			NumericKind::Current=>(&mut self.current_text,"A",maximum_current_description(self.model.mode,self.model.dual_mode)),
		};
		let mut commit=false;
		ui.horizontal(|ui|
		{
			let response=ui.add_sized([ui.available_width()-38.0,31.0],egui::TextEdit::singleline(text)
				.align(egui::Align2::RIGHT_CENTER).font(egui::TextStyle::Heading).background_color(CONTROL));
			ui.label(RichText::new(unit).size(20.0));
			commit=response.lost_focus() && ui.input(|input|input.key_pressed(egui::Key::Enter));
		});
		ui.label(RichText::new(label).monospace().size(10.0));
		ui.horizontal(|ui|
		{
			ui.add_space((ui.available_width()-66.0).max(0.0));
			if small_arrow(ui,"▲").clicked(){self.adjust_value(kind,1);}
			if small_arrow(ui,"▼").clicked(){self.adjust_value(kind,-1);}
		});
		if commit{self.apply_setpoints();}
	}

	fn adjust_value(&mut self,kind:NumericKind,direction:i32)
	{
		match kind
		{
			NumericKind::Voltage=>
			{
				let value=parse_scaled(&self.voltage_text,100).unwrap_or(0);
				let step=if self.step_milli{1}else{100};
				self.voltage_text=format_voltage((value+direction*step).max(0));
			},
			NumericKind::Current=>
			{
				let value=parse_scaled(&self.current_text,1000).unwrap_or(0);
				let step=if self.step_milli{1}else{1000};
				self.current_text=format_current((value+direction*step).max(0));
			},
		}
		self.apply_setpoints();
	}

	fn multiplier_buttons(&mut self,ui:&mut egui::Ui)
	{
		if multiplier_button(ui,"1",!self.step_milli).clicked(){self.step_milli=false;}
		if multiplier_button(ui,"m",self.step_milli).clicked(){self.step_milli=true;}
	}

	fn status(&self,ui:&mut egui::Ui)
	{
		let error=self.last_error_code.is_some() || self.model.error_detail().is_some();
		let response=ui.label(RichText::new(self.status_text()).monospace().size(10.0)
			.color(if error{RED}else{Color32::WHITE}));
		if let Some(detail)=self.status_detail(){response.on_hover_text(detail);}
	}

	fn dialogs(&mut self,context:&egui::Context)
	{
		if self.show_about
		{
			egui::Window::new("Autor").open(&mut self.show_about).resizable(false).show(context,|ui|
			{
				ui.label("Korad KA3005P");
				ui.label(format!("Wersja {APP_VERSION}"));
				ui.label("Projekt rozwijany z Codex GPT-5.6-Sol");
			});
		}
		if self.show_license
		{
			egui::Window::new("Licencja").open(&mut self.show_license).default_size([560.0,420.0]).show(context,|ui|
			{
				egui::ScrollArea::vertical().show(ui,|ui|{ui.label(include_str!("../../LICENSE"));});
			});
		}
		if self.show_ai
		{
			egui::Window::new("Sterowanie AI").open(&mut self.show_ai).resizable(false).show(context,|ui|
			{
				if let Some(server)=&self.server
				{
					ui.label(format!("API: http://127.0.0.1:{}",server.port()));
					ui.separator();
					token_row(ui,"Observer",server.viewer_token());
					token_row(ui,"Operator",server.operator_token());
					token_row(ui,"Expert",server.expert_token());
				}
				else{ui.colored_label(RED,"API nie zostało uruchomione");}
			});
		}
	}

	fn chart_window(&mut self,context:&egui::Context)
	{
		if !self.show_chart{return;}
		let history=self.model.history.clone();
		let offline=!self.primary_snapshot().output_on.unwrap_or(false);
		let title=format!("Wykres - Korad KA3005P v{APP_VERSION}");
		let size=if self.model.mode == AppMode::Single{[760.0,360.0]}else{[920.0,460.0]};
		context.show_viewport_immediate(
			egui::ViewportId::from_hash_of("korad_chart"),
			egui::ViewportBuilder::default().with_title(title).with_inner_size(size).with_min_inner_size(size).with_max_inner_size(size),
			|ui,_class|
			{
				if ui.input(|input|input.viewport().close_requested()){self.show_chart=false;}
				egui::Frame::new().fill(BACKGROUND).inner_margin(6.0).show(ui,|ui|
				{
					egui::MenuBar::new().ui(ui,|ui|
					{
						ui.menu_button("Widok",|ui|
						{
							ui.checkbox(&mut self.chart.show_current,"Prąd");
							ui.checkbox(&mut self.chart.show_voltage,"Napięcie");
							ui.separator();
							if ui.button("Wyczyść").clicked(){self.model.clear_history();ui.close();}
						});
						ui.menu_button("Zapisz jako",|ui|
						{
							if ui.button("Napięcie CSV").clicked(){self.export_csv(ExportKind::Voltage);ui.close();}
							if ui.button("Prąd CSV").clicked(){self.export_csv(ExportKind::Current);ui.close();}
							if ui.button("Napięcie i prąd CSV").clicked(){self.export_csv(ExportKind::Both);ui.close();}
						});
					});
					ui.horizontal(|ui|
					{
						let first=self.primary_snapshot();
						ui.label(value_text(format_current(first.measured_current_thousandths.unwrap_or(0))," A",RED));
						ui.label(value_text(format_voltage(first.measured_voltage_hundredths.unwrap_or(0))," V",GREEN));
						ui.with_layout(egui::Layout::right_to_left(egui::Align::Center),|ui|
						{
							if standard_button(ui,if offline{"OFF"}else{"ON"},72.0).clicked(){self.toggle_output();}
							if offline && !history.is_empty()
							{
								for index in (0..2).rev()
								{
									let color=if index == 0{ORANGE}else{GREEN};
									let active=self.chart.selected_cursor == Some(index);
									let button=egui::Button::new(RichText::new("Kursor").color(if active{Color32::BLACK}else{Color32::WHITE}))
										.fill(if active{color}else{CONTROL}).stroke(Stroke::new(1.0,Color32::from_gray(60)));
									if ui.add_sized([72.0,27.0],button).clicked()
									{
										self.chart.selected_cursor=if active{None}else{Some(index)};
									}
								}
							}
						});
					});
					let voltage:PlotPoints=history.iter().map(|point|[point.time_seconds,point.first_voltage]).collect();
					let current:PlotPoints=history.iter().map(|point|[point.time_seconds,point.first_current]).collect();
					let response=Plot::new("korad_chart_plot").allow_zoom(true).allow_drag(true).allow_scroll(true)
						.show_background(true).show_grid(true).x_axis_label("Czas").height((ui.available_height()-24.0).max(120.0)).show(ui,|plot_ui|
					{
						if self.chart.show_current{plot_ui.line(Line::new("Prąd",current).color(RED));}
						if self.chart.show_voltage{plot_ui.line(Line::new("Napięcie",voltage).color(GREEN));}
						for (index,value) in self.chart.cursors.iter().enumerate()
						{
							if let Some(x)=value
							{
								plot_ui.vline(VLine::new(format!("K{}",index+1),*x).color(if index == 0{ORANGE}else{GREEN}));
							}
						}
					});
					if response.response.clicked() && offline &&
						let (Some(index),Some(position))=(self.chart.selected_cursor,response.response.interact_pointer_pos())
					{
						self.chart.cursors[index]=Some(response.transform.value_from_position(position).x);
					}
					self.status(ui);
				});
			},
		);
	}

	fn export_csv(&mut self,kind:ExportKind)
	{
		let Some(path)=rfd::FileDialog::new().add_filter("CSV",&["csv"]).set_file_name("korad.csv").save_file() else{return;};
		let result=(||->Result<(),String>
		{
			let mut file=std::fs::File::create(path).map_err(|error|error.to_string())?;
			let dual=self.model.mode == AppMode::Dual;
			let header=match (kind,dual)
			{
				(ExportKind::Voltage,false)=>"czas_s;napiecie_V\n",
				(ExportKind::Current,false)=>"czas_s;prad_A\n",
				(ExportKind::Both,false)=>"czas_s;napiecie_V;prad_A\n",
				(ExportKind::Voltage,true)=>"czas_s;P1_napiecie_V;P2_napiecie_V\n",
				(ExportKind::Current,true)=>"czas_s;P1_prad_A;P2_prad_A\n",
				(ExportKind::Both,true)=>"czas_s;P1_napiecie_V;P1_prad_A;P2_napiecie_V;P2_prad_A\n",
			};
			file.write_all(header.as_bytes()).map_err(|error|error.to_string())?;
			for point in &self.model.history
			{
				file.write_all(csv_line(kind,dual,point).as_bytes()).map_err(|error|error.to_string())?;
			}
			Ok(())
		})();
		if let Err(error)=result{self.set_error(&error);}
	}
}

impl eframe::App for KoradApp
{
	fn ui(&mut self,ui:&mut egui::Ui,_frame:&mut eframe::Frame)
	{
		self.process_responses();
		self.periodic_poll(ui.ctx());
		egui::Frame::new().fill(BACKGROUND).inner_margin(0.0).show(ui,|ui|
		{
			egui::Frame::new().fill(DARK).show(ui,|ui|self.menu_bar(ui));
			ui.add_space(4.0);
			egui::Frame::new().inner_margin(egui::Margin::symmetric(12,4)).show(ui,|ui|
			{
				match self.model.mode
				{
					AppMode::Single=>self.single_panel(ui),
					AppMode::Dual=>self.dual_panel(ui),
				}
			});
		});
		self.dialogs(ui.ctx());
		self.chart_window(ui.ctx());
	}
}

impl Drop for KoradApp
{
	fn drop(&mut self)
	{
		self.hub.begin_shutdown();
		for port in self.hub.connected_ports(){let _=self.hub.disconnect(&port);}
		let _=self.request_tx.send(WorkerRequest::Stop);
		if let Some(worker)=self.worker.take(){let _=worker.join();}
		self.server.take();
	}
}

#[derive(Clone,Copy)]
enum NumericKind{Voltage,Current}

#[derive(Clone,Copy)]
enum ExportKind{Voltage,Current,Both}

fn worker_loop(hub:Arc<DeviceHub>,request_rx:mpsc::Receiver<WorkerRequest>,response_tx:mpsc::Sender<WorkerResponse>,context:egui::Context)
{
	let expert=AccessPolicy::new(Role::Expert,3100,5100);
	while let Ok(request)=request_rx.recv()
	{
		let response=match request
		{
			WorkerRequest::RefreshPorts=>WorkerResponse::Ports(DeviceHub::available_ports()),
			WorkerRequest::Connect(ports)=>WorkerResponse::Connected(connect_ports(&hub,&expert,&ports)),
			WorkerRequest::Disconnect(ports)=>WorkerResponse::Disconnected(disconnect_ports(&hub,&ports)),
			WorkerRequest::SetSingle(port,voltage,current)=>WorkerResponse::Updated(set_single(&hub,&expert,port,voltage,current)),
			WorkerRequest::Execute(port,command)=>WorkerResponse::Updated(
				hub.execute(&port,&expert,command).map(|(_,snapshot)|vec![(port,snapshot)])),
			WorkerRequest::DualOutput(first,second,enabled)=>WorkerResponse::Updated(
				hub.dual_output(&first,&second,&expert,enabled).map(|snapshots|vec![(first,snapshots[0].clone()),(second,snapshots[1].clone())])),
			WorkerRequest::DualSetpoints(first,second,mode,voltage,current)=>WorkerResponse::Updated(
				hub.dual_setpoints(&first,&second,mode,voltage,current,&expert).map(|snapshots|vec![(first,snapshots[0].clone()),(second,snapshots[1].clone())])),
			WorkerRequest::Poll(ports)=>WorkerResponse::Updated(poll_ports(&hub,&ports)),
			WorkerRequest::Stop=>break,
		};
		if response_tx.send(response).is_err(){break;}
		context.request_repaint();
	}
}

fn connect_ports(hub:&DeviceHub,policy:&AccessPolicy,ports:&[String])->Result<Vec<(String,Snapshot)>,String>
{
	let mut connected_now:Vec<String>=Vec::new();
	let mut snapshots=Vec::new();
	for port in ports
	{
		if !hub.connected_ports().contains(port)
		{
			if let Err(error)=hub.connect(port)
			{
				for item in connected_now{let _=hub.disconnect(&item);}
				return Err(error);
			}
			connected_now.push(port.clone());
		}
		for command in [Command::Identify,Command::ReadSetVoltage,Command::ReadSetCurrent]{hub.execute(port,policy,command)?;}
		snapshots.push((port.clone(),hub.poll(port)?));
	}
	Ok(snapshots)
}

fn disconnect_ports(hub:&DeviceHub,ports:&[String])->Result<Vec<String>,String>
{
	let mut disconnected=Vec::new();
	let mut errors=Vec::new();
	for port in ports
	{
		match hub.disconnect(port)
		{
			Ok(())=>disconnected.push(port.clone()),
			Err(error)=>errors.push(format!("{port}: {error}")),
		}
	}
	if errors.is_empty(){Ok(disconnected)}else{Err(errors.join("; "))}
}

fn set_single(hub:&DeviceHub,policy:&AccessPolicy,port:String,voltage:i32,current:i32)->Result<Vec<(String,Snapshot)>,String>
{
	hub.execute(&port,policy,Command::SetVoltage(voltage))?;
	let (_,snapshot)=hub.execute(&port,policy,Command::SetCurrent(current))?;
	Ok(vec![(port,snapshot)])
}

fn poll_ports(hub:&DeviceHub,ports:&[String])->Result<Vec<(String,Snapshot)>,String>
{
	let connected=hub.connected_ports();
	let mut snapshots=Vec::new();
	for port in ports
	{
		if connected.contains(port){snapshots.push((port.clone(),hub.poll(port)?));}
	}
	Ok(snapshots)
}

fn configure_style(context:&egui::Context)
{
	let mut visuals=egui::Visuals::dark();
	visuals.panel_fill=BACKGROUND;
	visuals.window_fill=BACKGROUND;
	visuals.widgets.inactive.bg_fill=CONTROL;
	visuals.widgets.inactive.fg_stroke=Stroke::new(1.0,Color32::WHITE);
	visuals.widgets.hovered.bg_fill=Color32::from_rgb(178,178,178);
	visuals.widgets.hovered.fg_stroke=Stroke::new(1.0,Color32::WHITE);
	visuals.widgets.active.bg_fill=Color32::from_rgb(118,118,118);
	visuals.selection.bg_fill=GREEN;
	visuals.selection.stroke=Stroke::new(1.0,Color32::BLACK);
	context.set_visuals(visuals);
	let mut style=(*context.style_of(egui::Theme::Dark)).clone();
	style.spacing.item_spacing=Vec2::new(5.0,4.0);
	style.spacing.button_padding=Vec2::new(8.0,3.0);
	context.set_style_of(egui::Theme::Dark,style);
}

fn port_combo(ui:&mut egui::Ui,id:&str,ports:&[String],selected:&mut String,enabled:bool)
{
	ui.add_enabled_ui(enabled,|ui|
	{
		let selected_text=if selected.is_empty(){"-".to_owned()}else{selected.clone()};
		egui::ComboBox::from_id_salt(id).width(88.0).selected_text(selected_text).show_ui(ui,|ui|
		{
			for port in ports{ui.selectable_value(selected,port.clone(),port);}
		});
	});
}

fn standard_button(ui:&mut egui::Ui,label:&str,width:f32)->egui::Response
{
	ui.add_sized([width,27.0],egui::Button::new(RichText::new(label).monospace().color(Color32::WHITE)).fill(CONTROL))
}

fn small_arrow(ui:&mut egui::Ui,label:&str)->egui::Response
{
	ui.add_sized([29.0,20.0],egui::Button::new(RichText::new(label).color(Color32::WHITE)).fill(CONTROL))
}

fn multiplier_button(ui:&mut egui::Ui,label:&str,active:bool)->egui::Response
{
	ui.add_sized([29.0,27.0],egui::Button::new(RichText::new(label).color(if active{Color32::BLACK}else{Color32::WHITE}))
		.fill(if active{GREEN}else{CONTROL}))
}

fn token_row(ui:&mut egui::Ui,label:&str,token:&str)
{
	ui.horizontal(|ui|
	{
		ui.label(format!("{label}:"));
		ui.add(egui::TextEdit::singleline(&mut token.to_owned()).desired_width(360.0).interactive(false));
		if ui.button("Kopiuj").clicked(){ui.ctx().copy_text(token.to_owned());}
	});
}

fn value_text(value:String,unit:&str,color:Color32)->RichText
{
	RichText::new(format!("{value}{unit}")).monospace().size(24.0).color(color)
}

fn physical_panel(ui:&mut egui::Ui,label:&str,snapshot:&Snapshot)
{
	egui::Frame::new().stroke(Stroke::new(1.0,Color32::from_gray(165))).inner_margin(5.0).show(ui,|ui|
	{
		ui.label(RichText::new(label).monospace().size(11.0));
		ui.label(RichText::new(format!("Nastawa: {} V / {} A",format_voltage(snapshot.set_voltage_hundredths.unwrap_or(0)),format_current(snapshot.set_current_thousandths.unwrap_or(0)))).monospace().size(10.0));
		ui.label(RichText::new(format!("Pomiar: {} V / {} A",format_voltage(snapshot.measured_voltage_hundredths.unwrap_or(0)),format_current(snapshot.measured_current_thousandths.unwrap_or(0)))).monospace().size(10.0));
	});
}

fn measurement_block(ui:&mut egui::Ui,label:&str,snapshot:&Snapshot,negative:bool)
{
	ui.with_layout(egui::Layout::right_to_left(egui::Align::TOP),|ui|
	{
		ui.vertical(|ui|
		{
			ui.label(RichText::new(label).monospace().size(12.0));
			let sign=if negative{"-"}else{""};
			ui.label(value_text(format!("{sign}{}",format_voltage(snapshot.measured_voltage_hundredths.unwrap_or(0)))," V",GREEN));
			ui.label(value_text(format!("{sign}{}",format_current(snapshot.measured_current_thousandths.unwrap_or(0)))," A",RED));
		});
	});
}

fn parse_scaled(text:&str,scale:i32)->Option<i32>
{
	let value=text.trim().replace(',',".").parse::<f64>().ok()?;
	if !value.is_finite() || value<0.0{return None;}
	Some((value*scale as f64).round() as i32)
}

fn format_voltage(value:i32)->String
{
	format!("{:.2}",value as f64/100.0).replace('.',",")
}

fn format_current(value:i32)->String
{
	format!("{:.3}",value as f64/1000.0).replace('.',",")
}

fn combine<F>(first:Option<i32>,second:Option<i32>,operation:F)->Option<i32>
where F:FnOnce(i32,i32)->i32
{
	Some(operation(first?,second?))
}

fn aggregate_voltage(mode:DualMode,first:&Snapshot,second:&Snapshot)->i32
{
	let first=first.measured_voltage_hundredths.unwrap_or(0);
	let second=second.measured_voltage_hundredths.unwrap_or(0);
	if mode == DualMode::Series{first+second}else{first}
}

fn aggregate_current(mode:DualMode,first:&Snapshot,second:&Snapshot)->i32
{
	let first=first.measured_current_thousandths.unwrap_or(0);
	let second=second.measured_current_thousandths.unwrap_or(0);
	if mode == DualMode::Parallel{first+second}else{first.min(second)}
}

fn resistance_text(snapshot:&Snapshot)->String
{
	let voltage=snapshot.measured_voltage_hundredths.unwrap_or(0) as f64/100.0;
	let current=snapshot.measured_current_thousandths.unwrap_or(0) as f64/1000.0;
	if current<=0.0{"--- Ω".into()}else{format!("{:.3} Ω",voltage/current)}
}

fn dual_mode_name(mode:DualMode)->&'static str
{
	match mode{DualMode::Series=>"Szeregowy",DualMode::Parallel=>"Równoległy",DualMode::Symmetric=>"Symetryczny"}
}

fn maximum_voltage_description(mode:AppMode,dual:DualMode)->&'static str
{
	if mode == AppMode::Dual && dual == DualMode::Series{"Max napięcie zestawu to 62V"}else{"Max napięcie zasilacza to 31V"}
}

fn maximum_current_description(mode:AppMode,dual:DualMode)->&'static str
{
	if mode == AppMode::Dual && dual == DualMode::Parallel{"Max prąd zestawu to 10,2A"}else{"Max prąd zasilacza to 5,1A"}
}

fn short_error_code(error:&str)->&'static str
{
	let lower=error.to_lowercase();
	if lower.contains("timeout") || lower.contains("czas"){"TOUT"}
	else if lower.contains("port") || lower.contains("com"){"PORT"}
	else if lower.contains("zakres") || lower.contains("nastaw") || lower.contains("wybierz"){"VAL"}
	else{"COMM"}
}

fn csv_line(kind:ExportKind,dual:bool,point:&crate::gui_model::MeasurementPoint)->String
{
	let value=match (kind,dual)
	{
		(ExportKind::Voltage,false)=>format!("{:.3};{:.2}\n",point.time_seconds,point.first_voltage),
		(ExportKind::Current,false)=>format!("{:.3};{:.3}\n",point.time_seconds,point.first_current),
		(ExportKind::Both,false)=>format!("{:.3};{:.2};{:.3}\n",point.time_seconds,point.first_voltage,point.first_current),
		(ExportKind::Voltage,true)=>format!("{:.3};{:.2};{:.2}\n",point.time_seconds,point.first_voltage,point.second_voltage.unwrap_or(0.0)),
		(ExportKind::Current,true)=>format!("{:.3};{:.3};{:.3}\n",point.time_seconds,point.first_current,point.second_current.unwrap_or(0.0)),
		(ExportKind::Both,true)=>format!("{:.3};{:.2};{:.3};{:.2};{:.3}\n",point.time_seconds,point.first_voltage,point.first_current,point.second_voltage.unwrap_or(0.0),point.second_current.unwrap_or(0.0)),
	};
	value.replace('.',",")
}

#[cfg(test)]
mod tests
{
	use super::*;

	#[test]
	fn parses_polish_and_dot_decimal_values()
	{
		assert_eq!(parse_scaled("12,50",100),Some(1250));
		assert_eq!(parse_scaled("1.001",1000),Some(1001));
		assert_eq!(parse_scaled("-1",100),None);
	}

	#[test]
	fn formats_device_precision_without_losing_native_step()
	{
		assert_eq!(format_voltage(1),"0,01");
		assert_eq!(format_current(1),"0,001");
	}
}
