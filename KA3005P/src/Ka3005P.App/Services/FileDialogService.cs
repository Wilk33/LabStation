using Microsoft.Win32;

namespace Ka3005P.App.Services;

public sealed class FileDialogService : IFileDialogService
{
	public ValueTask<string?> ChooseSavePathAsync(
		string suggestedFileName,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		SaveFileDialog dialog=new()
		{
			AddExtension=true,
			DefaultExt=".csv",
			FileName=suggestedFileName,
			Filter="Plik CSV (*.csv)|*.csv|Wszystkie pliki (*.*)|*.*",
			OverwritePrompt=true
		};
		return ValueTask.FromResult(dialog.ShowDialog() == true
			? dialog.FileName
			: null);
	}
}
