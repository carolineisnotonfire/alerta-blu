using AlertaBlu.Domain;

namespace AlertaBlu.Application;

/// <summary>Latest temperature reading from the AlertaBLU station feed.</summary>
/// <remarks>Reference type so it can flow through <see cref="SectionResult{T}"/>.</remarks>
public sealed record TemperatureReading(double ValueC, DateTimeOffset TimeLocal);

/// <summary>Today's forecast minimum/maximum scraped from the "detalhada" page.</summary>
public sealed record DayExtremes(double? MinC, double? MaxC);

/// <summary>Sensação térmica and humidity from Open-Meteo.</summary>
public sealed record ApparentConditions(double? FeelsLikeC, int? HumidityPercent);

/// <summary>Forecast sensação térmica and humidity for one future day, from Open-Meteo.</summary>
public sealed record DailyConditions(DateOnly Date, double? FeelsLikeC, int? HumidityPercent);

/// <summary>
/// The "detalhada" page split into the two cards it feeds. The two halves fail independently:
/// losing the forecast blocks must not cost today's extremes, or vice versa, since they come from
/// unrelated parts of the same page.
/// </summary>
public sealed record DetalhadaResult(SectionResult<IReadOnlyList<DailyForecast>> Forecast, DayExtremes? Extremes);

/// <summary>
/// The Open-Meteo response split into today's conditions and the per-day figures. <see cref="Daily"/>
/// is best-effort (an empty list, never an error) since it is a decorative addition to the forecast
/// chips and must never cost the hero card its sensação térmica.
/// </summary>
public sealed record OpenMeteoResult(SectionResult<ApparentConditions> Apparent, IReadOnlyList<DailyConditions> Daily);

/// <summary>
/// Per-section fetch and parse of the upstream sources. Pure I/O: no cross-section composition, no
/// caching, no fusing of independent sources into one card — that is <see cref="LoadDashboardUseCase"/>'s
/// job. Every method degrades a failure to a <see cref="SectionResult{T}.Fail(string)"/> rather than
/// throwing, so one source being down never takes any other section with it.
/// </summary>
public interface IAlertaBluGateway
{
    Task<SectionResult<TemperatureReading>> GetTemperatureAsync(CancellationToken cancellationToken = default);

    Task<DetalhadaResult> GetDetalhadaAsync(CancellationToken cancellationToken = default);

    Task<OpenMeteoResult> GetOpenMeteoAsync(CancellationToken cancellationToken = default);

    Task<SectionResult<RiverLevel>> GetRiverLevelAsync(CancellationToken cancellationToken = default);

    Task<SectionResult<IReadOnlyList<RiverThreshold>>> GetRiverThresholdsAsync(CancellationToken cancellationToken = default);

    Task<SectionResult<IReadOnlyList<CotaEnchente>>> GetCotasAsync(CancellationToken cancellationToken = default);

    Task<SectionResult<IReadOnlyList<Barragem>>> GetBarragensAsync(CancellationToken cancellationToken = default);
}
