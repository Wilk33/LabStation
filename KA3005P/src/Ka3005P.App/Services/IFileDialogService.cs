namespace Ka3005P.App.Services;

public interface IFileDialogService
{
	ValueTask<string?> ChooseSavePathAsync(
		string suggestedFileName,
		CancellationToken cancellationToken);
}
