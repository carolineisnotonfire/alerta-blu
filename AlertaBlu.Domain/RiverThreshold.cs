namespace AlertaBlu.Domain;

/// <summary>
/// One official river-level band ("Normalidade", "Atenção", "Alerta"…), shown in the expanded
/// river card.
/// </summary>
/// <remarks>
/// Built from <c>static/data/nivel_oficial.json</c>, whose <c>condicoes</c> array publishes only
/// the level at which each condition <em>starts</em>. The displayable range is therefore derived
/// from consecutive entries, and the highest band is open-ended. Display formatting
/// (<c>RangeDisplay</c>) lives on the Presentation-layer <c>RiverThresholdViewModel</c> instead.
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

    /// <summary>Whether a reading falls in this band; the upper bound is exclusive.</summary>
    public bool Contains(double level) =>
        level >= MinMeters && (MaxMeters is not { } max || level < max);

    /// <summary>
    /// Flags the band holding <paramref name="level"/>, for the highlighted row in the expanded
    /// river card. Returns the bands untouched when there is no reading to place.
    /// </summary>
    /// <remarks>
    /// A reading below the lowest published band (the feed starts at 0 m, but a negative or
    /// re-baselined reading is possible) highlights the lowest band rather than nothing at all.
    /// </remarks>
    public static IReadOnlyList<RiverThreshold> HighlightCurrent(
        IReadOnlyList<RiverThreshold> thresholds,
        double? level)
    {
        if (level is not { } meters || thresholds.Count == 0)
        {
            return thresholds;
        }

        var currentIndex = -1;
        for (var i = 0; i < thresholds.Count; i++)
        {
            if (thresholds[i].Contains(meters))
            {
                currentIndex = i;
                break;
            }
        }

        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var flagged = new RiverThreshold[thresholds.Count];
        for (var i = 0; i < thresholds.Count; i++)
        {
            flagged[i] = thresholds[i] with { IsCurrent = i == currentIndex };
        }

        return flagged;
    }
}
