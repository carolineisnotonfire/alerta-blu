using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AlertaBlu.ViewModels;

/// <summary>
/// Minimal <see cref="INotifyPropertyChanged"/> plumbing shared by the page view model and the
/// per-row view models behind the forecast chips and the dam cards.
/// </summary>
/// <remarks>
/// Hand-rolled rather than inherited from an MVVM framework, consistently with the rest of the
/// app: three small view models do not justify a source generator and a package dependency.
/// </remarks>
public abstract class ObservableBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Assigns and notifies; returns false when the value was already equal.</summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
