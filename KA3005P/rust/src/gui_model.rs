use crate::control::Snapshot;
use crate::dual::DualMode;
use std::collections::{HashMap,HashSet,VecDeque};

#[derive(Clone,Copy,Debug,Default,PartialEq,Eq)]
pub enum AppMode
{
	#[default]
	Single,
	Dual,
}

#[derive(Clone,Debug,Default)]
pub struct MeasurementPoint
{
	pub time_seconds:f64,
	pub first_voltage:f64,
	pub first_current:f64,
	pub second_voltage:Option<f64>,
	pub second_current:Option<f64>,
}

pub struct GuiModel
{
	pub mode:AppMode,
	pub dual_mode:DualMode,
	pub ports:Vec<String>,
	pub selected_first:String,
	pub selected_second:String,
	pub connected:HashSet<String>,
	pub snapshots:HashMap<String,Snapshot>,
	pub history:VecDeque<MeasurementPoint>,
	pub measurement_seconds:f64,
}

impl Default for GuiModel
{
	fn default()->Self
	{
		Self
		{
			mode:AppMode::Single,
			dual_mode:DualMode::Series,
			ports:Vec::new(),
			selected_first:String::new(),
			selected_second:String::new(),
			connected:HashSet::new(),
			snapshots:HashMap::new(),
			history:VecDeque::new(),
			measurement_seconds:0.0,
		}
	}
}

impl GuiModel
{
	pub fn window_size(&self)->[f32;2]
	{
		match self.mode
		{
			AppMode::Single=>[300.0,360.0],
			AppMode::Dual=>[380.0,460.0],
		}
	}

	pub fn set_ports(&mut self,mut ports:Vec<String>)
	{
		ports.sort();
		self.ports=ports;
		if self.selected_first.is_empty()
		{
			self.selected_first=self.ports.first().cloned().unwrap_or_default();
		}
		if self.selected_second.is_empty()
		{
			self.selected_second=self.ports.iter()
				.find(|port|**port != self.selected_first)
				.cloned()
				.unwrap_or_default();
		}
	}

	pub fn mark_connected(&mut self,port:&str,snapshot:Snapshot)
	{
		self.connected.insert(port.to_owned());
		self.snapshots.insert(port.to_owned(),snapshot);
	}

	pub fn mark_disconnected(&mut self,port:&str)
	{
		self.connected.remove(port);
		self.snapshots.remove(port);
	}

	pub fn is_connected(&self,port:&str)->bool
	{
		self.connected.contains(port)
	}

	pub fn accept_snapshot(&mut self,port:&str,snapshot:Snapshot,elapsed_seconds:f64)
	{
		if self.selected_first.is_empty()
		{
			self.selected_first=port.to_owned();
		}
		self.connected.insert(port.to_owned());
		let output_on=snapshot.output_on == Some(true);
		self.snapshots.insert(port.to_owned(),snapshot);
		if port != self.selected_first || !output_on
		{
			return;
		}
		self.measurement_seconds+=elapsed_seconds.max(0.0);
		let first=self.snapshots.get(&self.selected_first).cloned().unwrap_or_default();
		let second=self.snapshots.get(&self.selected_second).cloned();
		self.history.push_back(MeasurementPoint
		{
			time_seconds:self.measurement_seconds,
			first_voltage:first.measured_voltage_hundredths.unwrap_or(0) as f64/100.0,
			first_current:first.measured_current_thousandths.unwrap_or(0) as f64/1000.0,
			second_voltage:second.as_ref().and_then(|item|item.measured_voltage_hundredths).map(|value|value as f64/100.0),
			second_current:second.as_ref().and_then(|item|item.measured_current_thousandths).map(|value|value as f64/1000.0),
		});
		let minimum=self.measurement_seconds-60.0;
		while self.history.front().is_some_and(|point|point.time_seconds<minimum)
		{
			self.history.pop_front();
		}
	}

	pub fn clear_history(&mut self)
	{
		self.history.clear();
		self.measurement_seconds=0.0;
	}

	pub fn status_text(&self)->String
	{
		let Some(snapshot)=self.snapshots.get(&self.selected_first) else
		{
			return "Status: OFFLINE".into();
		};
		if let Some(code)=&snapshot.error_code
		{
			return format!("Status: {code}");
		}
		if snapshot.output_on == Some(true){"Status: ON".into()}else{"Status: OFF".into()}
	}

	pub fn error_detail(&self)->Option<&str>
	{
		self.snapshots.get(&self.selected_first)?.error_detail.as_deref()
	}
}
