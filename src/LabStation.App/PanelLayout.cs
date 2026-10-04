namespace LabStation.App;

public sealed record PanelLayout(
	double OscilloscopeWidth,
	double GeneratorWidth,
	double RightColumnWidth,
	double MultimeterHeight,
	double ElectronicLoadHeight)
{
	public static PanelLayout Default { get; }=new(0.53,0.18,0.29,0.40,0.60);
}
