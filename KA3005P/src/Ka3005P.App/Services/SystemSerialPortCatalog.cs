using System.IO.Ports;

namespace Ka3005P.App.Services;

public sealed class SystemSerialPortCatalog : ISerialPortCatalog
{
	public IReadOnlyList<string> GetPortNames()
	{
		return Normalize(SerialPort.GetPortNames());
	}

	public static IReadOnlyList<string> Normalize(IEnumerable<string> names)
	{
		ArgumentNullException.ThrowIfNull(names);
		return names
			.Select(name=>name.Trim().ToUpperInvariant())
			.Where(name=>name.Length>0)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(name=>name,PortNameComparer.Instance)
			.ToArray();
	}

	private sealed class PortNameComparer : IComparer<string>
	{
		public static PortNameComparer Instance { get; }=new();

		public int Compare(string? first,string? second)
		{
			if(ReferenceEquals(first,second))
			{
				return 0;
			}
			if(first is null)
			{
				return -1;
			}
			if(second is null)
			{
				return 1;
			}
			if(TryGetComNumber(first,out int firstNumber) &&
				TryGetComNumber(second,out int secondNumber))
			{
				int numeric=firstNumber.CompareTo(secondNumber);
				if(numeric != 0)
				{
					return numeric;
				}
			}
			return StringComparer.OrdinalIgnoreCase.Compare(first,second);
		}

		private static bool TryGetComNumber(string name,out int number)
		{
			number=0;
			return name.StartsWith("COM",StringComparison.OrdinalIgnoreCase) &&
				int.TryParse(name.AsSpan(3),out number);
		}
	}
}
