namespace Sdm3000.Core;

public static class MeasurementProfiles
{
	public static MeasurementProfile For(MeasurementFunction function)
	{
		return function switch
		{
			MeasurementFunction.VoltageDc=>new("Napięcie DC","V DC","Vdc","V"),
			MeasurementFunction.VoltageAc=>new("Napięcie AC","V AC","Vrms","V"),
			MeasurementFunction.CurrentDc=>new("Prąd DC","A DC","Idc","A"),
			MeasurementFunction.CurrentAc=>new("Prąd AC","A AC","Irms","A"),
			MeasurementFunction.Resistance2Wire=>new("Rezystancja","Ω","R","Ω"),
			MeasurementFunction.Resistance4Wire=>new("Rezystancja 4W","Ω 4W","R","Ω"),
			MeasurementFunction.Capacitance=>new("Pojemność","F","C","F"),
			MeasurementFunction.Continuity=>new("Ciągłość","Sig","R","Ω"),
			MeasurementFunction.Diode=>new("Dioda","Diod","Vf","V"),
			MeasurementFunction.Frequency=>new("Częstotliwość","Hz","f","Hz"),
			MeasurementFunction.Period=>new("Okres","s","T","s"),
			MeasurementFunction.Temperature=>new("Temperatura","°C","T","°C"),
			_=>new("Pomiar","?","Wartość","")
		};
	}
}
