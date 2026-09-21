using AlertaBlu.Domain;

namespace AlertaBlu.ViewModels;

/// <summary>
/// One dam row, wrapping the immutable <see cref="Barragem"/> record with the expand/collapse
/// state that each card owns independently.
/// </summary>
public sealed class DamCardViewModel : ObservableBase
{
    private bool _isExpanded;

    public DamCardViewModel(Barragem data)
    {
        Data = data;
        ToggleCommand = new Command(() => IsExpanded = !IsExpanded);
    }

    public Barragem Data { get; }

    public Command ToggleCommand { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }
}
