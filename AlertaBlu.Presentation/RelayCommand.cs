using System.Windows.Input;

namespace AlertaBlu.ViewModels;

/// <summary>
/// Minimal parameterless <see cref="ICommand"/>, so the view models have no dependency on
/// <c>Microsoft.Maui.Controls.Command</c> and this project can stay a plain net10.0 library.
/// Every command in this app is always executable, so <see cref="CanExecute"/> is always true and
/// <see cref="CanExecuteChanged"/> is never raised.
/// </summary>
public sealed class RelayCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();
}
