using AlertaBlu.Domain.Common;

namespace AlertaBlu.Domain;

/// <summary>Reservoir status for one of the upstream flood-control dams.</summary>
public sealed record Barragem
{
    public required string Estacao { get; init; }

    /// <summary>Local timestamp of the reading, as published (already UTC-3).</summary>
    public DateTime? ReadingTime { get; init; }

    /// <summary>Percentage of flood-storage capacity in use.</summary>
    public double? CapacityPercent { get; init; }

    public int? GatesOpen { get; init; }

    public int? GatesClosed { get; init; }

    public string CapacityDisplay =>
        CapacityPercent is { } value ? $"{PtBr.Format(value, 2)}%" : "--";

    /// <summary>0-1 value for the progress bar, clamped so bad data cannot break the layout.</summary>
    public double CapacityFraction => Math.Clamp((CapacityPercent ?? 0d) / 100d, 0d, 1d);

    public string TimeDisplay => ReadingTime?.ToString("dd/MM HH:mm") ?? "--";

    public bool HasGates => GatesOpen is not null || GatesClosed is not null;

    public string GatesDisplay => HasGates
        ? $"Comportas abertas: {GatesOpen?.ToString() ?? "?"}  ·  fechadas: {GatesClosed?.ToString() ?? "?"}"
        : "Comportas: sem informação";

    /// <summary>
    /// Whether the dam is in an operationally notable state. Open floodgates are the real signal
    /// AlertaBLU publishes; a volume percentage on its own says nothing about intent, so the
    /// status tag is driven by the gates rather than by a capacity threshold.
    /// </summary>
    public bool IsAlert => GatesOpen is > 0;

    /// <summary>Status tag shown next to the dam name.</summary>
    public string StatusLabel => IsAlert ? "Atenção" : "Regular";

    /// <summary>Detail line revealed when the dam card is expanded.</summary>
    public string DetailDisplay => $"Volume: {CapacityDisplay}  ·  {GatesDisplay}";
}
