using System.Globalization;

namespace Sdm3000.Core;

public static class MeasurementCsvWriter
{
	public static void Write(
		TextWriter writer,
		IEnumerable<MeasurementSnapshot> snapshots)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(snapshots);
		writer.WriteLine("Czas;Funkcja;Etykieta;Wartosc;Jednostka;Zakres;Stan;PunktyPamieci");
		foreach(MeasurementSnapshot snapshot in snapshots)
		{
			MeasurementProfile profile=MeasurementProfiles.For(
				snapshot.Configuration.Function);
			writer.Write(snapshot.Timestamp.ToString("O",CultureInfo.InvariantCulture));
			writer.Write(';');
			writer.Write(snapshot.Configuration.Function);
			writer.Write(';');
			writer.Write(profile.PrimaryLabel);
			writer.Write(';');
			writer.Write(snapshot.Reading.Value?.ToString("R",CultureInfo.InvariantCulture));
			writer.Write(';');
			writer.Write(profile.Unit);
			writer.Write(';');
			writer.Write(snapshot.Configuration.Range?.ToString("R",CultureInfo.InvariantCulture));
			writer.Write(';');
			writer.Write(snapshot.Reading.State == ReadingState.Overload ? "OVERLOAD" : "VALUE");
			writer.Write(';');
			writer.WriteLine(snapshot.StoredPoints.ToString(CultureInfo.InvariantCulture));
		}
	}
}
