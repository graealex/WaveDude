using System.Windows.Input;

namespace WaveDudeNext.WPF.Commands;

/// <summary>
/// <see cref="ICommand"/> over a delegate with a typed parameter.
/// </summary>
public sealed class RelayCommand<T> : ICommand
{
	private readonly Action<T> _execute;
	private readonly Predicate<T>? _canExecute;

	public RelayCommand(Action<T> execute, Predicate<T>? canExecute = null)
	{
		_execute = execute;
		_canExecute = canExecute;
	}

	/// <inheritdoc/>
	public event EventHandler? CanExecuteChanged
	{
		add => CommandManager.RequerySuggested += value;
		remove => CommandManager.RequerySuggested -= value;
	}

	/// <inheritdoc/>
	public bool CanExecute(object? parameter) =>
		parameter is T value && (_canExecute == null || _canExecute(value));

	/// <inheritdoc/>
	public void Execute(object? parameter)
	{
		if (parameter is T value)
		{
			_execute(value);
		}
	}
}
