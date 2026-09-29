namespace Ka3005P.Core.Device;

public sealed class DeviceCommunicationException : Exception
{
	public DeviceCommunicationException(string message)
		: base(message)
	{
	}

	public DeviceCommunicationException(string message,Exception innerException)
		: base(message,innerException)
	{
	}
}
