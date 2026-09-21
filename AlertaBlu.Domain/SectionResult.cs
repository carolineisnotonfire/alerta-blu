namespace AlertaBlu.Domain;

/// <summary>
/// Outcome of loading one dashboard section. Each section is fetched from a different live
/// endpoint, so failures are represented as data rather than exceptions: one section going down
/// must never blank out the rest of the page.
/// </summary>
/// <remarks>
/// Constrained to reference types on purpose: for an unconstrained <c>T</c>, <c>T?</c> on a
/// value type means <c>default(T)</c> rather than <c>Nullable&lt;T&gt;</c>, which would make the
/// null check in <see cref="HasValue"/> silently always true.
/// </remarks>
public readonly record struct SectionResult<T>(T? Value, string? Error, DateTimeOffset? StaleAsOf)
    where T : class
{
    public static SectionResult<T> Ok(T value) => new(value, null, null);

    public static SectionResult<T> Fail(string error) => new(default, error, null);

    /// <summary>
    /// A cached value served because the live fetch failed (or was skipped). Carries no error, so
    /// the section still renders normally; <see cref="StaleAsOf"/> is when this value was last
    /// genuinely fresh, for a "dados de HH:mm" indicator.
    /// </summary>
    public static SectionResult<T> Stale(T value, DateTimeOffset asOf) => new(value, null, asOf);

    /// <summary>Not yet loaded: neither a value nor an error.</summary>
    public static SectionResult<T> Empty => default;

    public bool HasValue => Value is not null && Error is null;

    public bool HasError => Error is not null;

    public bool IsStale => StaleAsOf is not null;
}

/// <summary>Everything the home screen needs, with per-section success/failure.</summary>
public sealed record DashboardSnapshot
{
    public SectionResult<CurrentWeather> Weather { get; init; }

    public SectionResult<IReadOnlyList<DailyForecast>> Forecast { get; init; }

    public SectionResult<RiverLevel> River { get; init; }

    /// <summary>
    /// Official level bands from <c>nivel_oficial.json</c>. A seventh independent source, so the
    /// river card still shows the current level when only the band list fails.
    /// </summary>
    public SectionResult<IReadOnlyList<RiverThreshold>> RiverThresholds { get; init; }

    public SectionResult<IReadOnlyList<CotaEnchente>> Cotas { get; init; }

    public SectionResult<IReadOnlyList<Barragem>> Barragens { get; init; }

    /// <summary>When the snapshot was assembled, for the "atualizado às" label.</summary>
    public DateTimeOffset LoadedAt { get; init; } = DateTimeOffset.Now;
}
