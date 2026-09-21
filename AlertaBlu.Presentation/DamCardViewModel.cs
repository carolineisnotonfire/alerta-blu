using System.Windows.Input;
using AlertaBlu.Domain;
using AlertaBlu.Domain.Common;

namespace AlertaBlu.ViewModels;

/// <summary>
/// One dam row, wrapping the immutable <see cref="Barragem"/> record with the expand/collapse
/// state that each card owns independently, and with the display formatting that used to live on
/// the Domain record itself.
/// </summary>
public sealed class DamCardViewModel : ObservableBase
{
    private bool _isExpanded;

    public DamCardViewModel(Barragem data)
    {
        Data = data;
        ToggleCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
    }

    public Barragem Data { get; }

    public ICommand ToggleCommand { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public string CapacityDisplay =>
        Data.CapacityPercent is { } value ? $"{PtBr.Format(value, 2)}%" : "--";

    /// <summary>0-1 value for the progress bar, clamped so bad data cannot break the layout.</summary>
    public double CapacityFraction => Math.Clamp((Data.CapacityPercent ?? 0d) / 100d, 0d, 1d);

    public string TimeDisplay => Data.ReadingTime?.ToString("dd/MM HH:mm") ?? "--";

    public bool HasGates => Data.GatesOpen is not null || Data.GatesClosed is not null;

    public string GatesDisplay => HasGates
        ? $"Comportas abertas: {Data.GatesOpen?.ToString() ?? "?"}  ·  fechadas: {Data.GatesClosed?.ToString() ?? "?"}"
        : "Comportas: sem informação";

    /// <summary>
    /// Whether the dam is in an operationally notable state. Open floodgates are the real signal
    /// AlertaBLU publishes; a volume percentage on its own says nothing about intent, so the
    /// status tag is driven by the gates rather than by a capacity threshold.
    /// </summary>
    public bool IsAlert => Data.GatesOpen is > 0;

    /// <summary>Status tag shown next to the dam name.</summary>
    public string StatusLabel => IsAlert ? "Atenção" : "Regular";

    /// <summary>Detail line revealed when the dam card is expanded.</summary>
    public string DetailDisplay => $"Volume: {CapacityDisplay}  ·  {GatesDisplay}";
}
