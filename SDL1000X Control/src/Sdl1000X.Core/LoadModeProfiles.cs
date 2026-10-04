namespace Sdl1000X.Core;

public static class LoadModeProfiles
{
	public static LoadModeProfile For(LoadMode mode)
	{
		return mode switch
		{
			LoadMode.ConstantCurrent=>new("CC","A",0,30,0.001,3),
			LoadMode.ConstantVoltage=>new("CV","V",0,150,0.001,3),
			LoadMode.ConstantPower=>new("CP","W",0,200,0.01,2),
			LoadMode.ConstantResistance=>new("CR","Ω",0.03,10000,0.001,3),
			LoadMode.Led=>new("LED","",0,0,0,3),
			_=>throw new ArgumentOutOfRangeException(nameof(mode))
		};
	}
}
