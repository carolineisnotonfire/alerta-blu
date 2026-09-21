namespace AlertaBlu.Domain;

/// <summary>
/// Outcome of evaluating whether the current river reading warrants alerting the user. Pure data:
/// deciding whether/how to deliver an alert (a push notification, a badge, …) is a separate,
/// platform-specific concern layered on top of this.
/// </summary>
public sealed record AlertEvaluation
{
    /// <summary>The band containing the current reading; null if it could not be determined.</summary>
    public RiverThreshold? CurrentBand { get; init; }

    /// <summary>
    /// True when the reading has moved into a higher-severity band than the last one observed.
    /// False (not true) when there is nothing to compare against yet, e.g. the very first
    /// evaluation after opening the app: the current band is still shown, just not flagged as a
    /// fresh escalation.
    /// </summary>
    public bool IsEscalation { get; init; }

    /// <summary>Watched streets whose flood threshold the current reading has now reached or exceeded.</summary>
    public IReadOnlyList<CotaEnchente> AffectedStreets { get; init; } = [];

    /// <summary>Whether this evaluation is worth surfacing to the user at all.</summary>
    public bool RequiresAlert => IsEscalation || AffectedStreets.Count > 0;
}

/// <summary>
/// Decides whether the latest river reading is alert-worthy. Pure and side-effect free, so it can
/// be evaluated on every refresh (or from a background task, once one exists) without any platform
/// dependency.
/// </summary>
public static class AlertPolicy
{
    /// <param name="current">The latest river-level reading.</param>
    /// <param name="bands">Official level bands (does not need to have <see cref="RiverThreshold.IsCurrent"/> pre-flagged).</param>
    /// <param name="previousBand">
    /// The band the previous reading fell in, or null if there is no prior observation (e.g. the
    /// first load this session). Used only to detect a fresh escalation.
    /// </param>
    /// <param name="watchedStreets">
    /// Streets the user has asked to be watched (e.g. their own address). Empty until the app has
    /// a way to mark one.
    /// </param>
    public static AlertEvaluation Evaluate(
        RiverLevel current,
        IReadOnlyList<RiverThreshold> bands,
        RiverThreshold? previousBand,
        IReadOnlyList<CotaEnchente> watchedStreets)
    {
        var currentBand = bands.FirstOrDefault(band => band.Contains(current.LevelMeters));

        var isEscalation = currentBand is not null
            && previousBand is not null
            && currentBand.MinMeters > previousBand.MinMeters;

        var affectedStreets = watchedStreets
            .Where(street => street.CotaMeters is { } threshold && current.LevelMeters >= threshold)
            .ToArray();

        return new AlertEvaluation
        {
            CurrentBand = currentBand,
            IsEscalation = isEscalation,
            AffectedStreets = affectedStreets,
        };
    }
}
