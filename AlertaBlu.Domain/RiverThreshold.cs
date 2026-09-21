using AlertaBlu.Domain.Common;

namespace AlertaBlu.Domain;

/// <summary>
/// One official river-level band ("Normalidade", "Atenção", "Alerta"…), shown in the expanded
/// river card.
/// </summary>
/// <remarks>
/// Built from <c>static/data/nivel_oficial.json</c>, whose <c>condicoes</c> array publishes only
/// the level at which each condition <em>starts</em>. The displayable range is therefore derived
/// from consecutive entries, and the highest band is open-ended.
/// </remarks>
public sealed record RiverThreshold
{
    public required string Label { get; init; }

    /// <summary>Level in metres at which this band starts (inclusive).</summary>
    public required double MinMeters { get; init; }

    /// <summary>Level at which the next band starts (exclusive); null for the highest band.</summary>
    public double? MaxMeters { get; init; }

    /// <summary>True when the latest reading falls inside this band.</summary>
    public bool IsCurrent { get; init; }

    /// <summary>Complement of <see cref="IsCurrent"/>, for the "not highlighted" visual state.</summary>
    public bool IsNotCurrent => !IsCurrent;

    public string RangeDisplay => MaxMeters is { } max
        ? MinMeters <= 0d
            ? $"até {PtBr.Format(max, 2)} m"
            : $"{PtBr.Format(MinMeters, 2)} – {PtBr.Format(max, 2)} m"
        : $"acima de {PtBr.Format(MinMeters, 2)} m";

    /// <summary>Whether a reading falls in this band; the upper bound is exclusive.</summary>
    public bool Contains(double level) =>
        level >= MinMeters && (MaxMeters is not { } max || level < max);
}
