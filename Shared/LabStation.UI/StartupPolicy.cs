namespace LabStation.UI;

public static class StartupPolicy
{
	public static bool AutoConnectAllowed=>
		!string.Equals(
			Environment.GetEnvironmentVariable(
				"LABSTATION_DISABLE_AUTO_CONNECT"),
			"1",
			StringComparison.Ordinal);
}
