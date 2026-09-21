namespace AlertaBlu.Domain;

/// <summary>
/// Reservoir status for one of the upstream flood-control dams. Display formatting lives on the
/// Presentation-layer <c>DamCardViewModel</c> instead.
/// </summary>
public sealed record Barragem
{
    public required string Estacao { get; init; }

    /// <summary>Local timestamp of the reading, as published (already UTC-3).</summary>
    public DateTime? ReadingTime { get; init; }

    /// <summary>Percentage of flood-storage capacity in use.</summary>
    public double? CapacityPercent { get; init; }

    public int? GatesOpen { get; init; }

    public int? GatesClosed { get; init; }
}
