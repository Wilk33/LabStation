using System.Windows.Media;

namespace LabStation.UI;

public sealed record ApplicationPresentation(
	string DisplayName,
	string AuthorName,
	string AuthorProfession,
	string AuthorEmail,
	string LicenseText,
	ImageSource? Icon);
