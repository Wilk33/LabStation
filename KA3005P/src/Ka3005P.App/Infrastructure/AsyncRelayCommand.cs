using System.Windows.Input;

namespace Ka3005P.App.Infrastructure;

public sealed class AsyncRelayCommand : ICommand
{
	private readonly Func<object?,Task> execute;
	private readonly Predicate<object?>? canExecute;
	private readonly Action<Exception> errorHandler;
	private bool isExecuting;

	public AsyncRelayCommand(
		Func<object?,Task> execute,
		Predicate<object?>? canExecute=null,
		Action<Exception>? errorHandler=null)
	{
		ArgumentNullException.ThrowIfNull(execute);
		this.execute=execute;
		this.canExecute=canExecute;
		this.errorHandler=errorHandler ?? (_=>{ });
	}

	public event EventHandler? CanExecuteChanged;

	public bool IsExecuting
	{
		get => isExecuting;
		private set
		{
			if(isExecuting == value)
			{
				return;
			}
			isExecuting=value;
			CanExecuteChanged?.Invoke(this,EventArgs.Empty);
		}
	}

	public bool CanExecute(object? parameter)
	{
		return !IsExecuting && (canExecute?.Invoke(parameter) ?? true);
	}

	public async void Execute(object? parameter)
	{
		await ExecuteAsync(parameter);
	}

	public async Task ExecuteAsync(object? parameter)
	{
		if(!CanExecute(parameter))
		{
			return;
		}

		IsExecuting=true;
		try
		{
			await execute(parameter);
		}
		catch(Exception exception)
		{
			errorHandler(exception);
		}
		finally
		{
			IsExecuting=false;
		}
	}

	public void RaiseCanExecuteChanged()
	{
		CanExecuteChanged?.Invoke(this,EventArgs.Empty);
	}
}
