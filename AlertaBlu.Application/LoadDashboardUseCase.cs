using AlertaBlu.Domain;

namespace AlertaBlu.Application;

/// <summary>
/// Loads the home screen's dashboard: fetches every section from <see cref="IAlertaBluGateway"/>
/// concurrently, fuses the independent sources behind the weather and forecast cards, falls back to
/// <see cref="IDashboardCache"/> for any section whose live fetch failed, and persists the result for
/// next time. Never throws for upstream failures; cancellation is the only exception that escapes.
/// </summary>
public sealed class LoadDashboardUseCase(IAlertaBluGateway gateway, IDashboardCache cache, IClock clock)
{
    /// <summary>
    /// Cotas rarely changes (street flood thresholds are edited maybe yearly) and is by far the
    /// heaviest download (~700 KB), so a cached copy this fresh is served without hitting the
    /// network at all.
    /// </summary>
    private static readonly TimeSpan CotasCacheTtl = TimeSpan.FromHours(24);

    private const string GenericWeatherError = "Não foi possível carregar os dados.";

    public async Task<DashboardSnapshot> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var cached = await cache.ReadAsync(cancellationToken).ConfigureAwait(false) ?? new DashboardSnapshot();

        var temperatureTask = gateway.GetTemperatureAsync(cancellationToken);
        var detalhadaTask = gateway.GetDetalhadaAsync(cancellationToken);
        var openMeteoTask = gateway.GetOpenMeteoAsync(cancellationToken);
        var riverTask = gateway.GetRiverLevelAsync(cancellationToken);
        var thresholdsTask = gateway.GetRiverThresholdsAsync(cancellationToken);
        var cotasTask = IsCotasCacheFreshEnough(cached)
            ? Task.FromResult(SectionResult<IReadOnlyList<CotaEnchente>>.Ok(cached.Cotas.Value!))
            : gateway.GetCotasAsync(cancellationToken);
        var barragensTask = gateway.GetBarragensAsync(cancellationToken);

        await Task.WhenAll(
            temperatureTask, detalhadaTask, openMeteoTask,
            riverTask, thresholdsTask, cotasTask, barragensTask)
            .ConfigureAwait(false);

        var temperature = await temperatureTask.ConfigureAwait(false);
        var detalhada = await detalhadaTask.ConfigureAwait(false);
        var openMeteo = await openMeteoTask.ConfigureAwait(false);

        // The stale-cache fallback for a failed live fetch, so one dropped request degrades to
        // "showing data from HH:mm" rather than a blank section.
        var river = WithCacheFallback(await riverTask.ConfigureAwait(false), cached.River);

        var snapshot = new DashboardSnapshot
        {
            Weather = WithCacheFallback(
                BuildWeather(temperature, openMeteo.Apparent, detalhada.Extremes), cached.Weather),
            Forecast = WithCacheFallback(
                MergeDailyConditions(detalhada.Forecast, openMeteo.Daily), cached.Forecast),
            River = river,
            RiverThresholds = WithCacheFallback(
                HighlightCurrentBand(await thresholdsTask.ConfigureAwait(false), river), cached.RiverThresholds),
            Cotas = WithCacheFallback(await cotasTask.ConfigureAwait(false), cached.Cotas),
            Barragens = WithCacheFallback(await barragensTask.ConfigureAwait(false), cached.Barragens),
            LoadedAt = clock.Now,
        };

        await cache.WriteAsync(snapshot, cancellationToken).ConfigureAwait(false);

        return snapshot;
    }

    private bool IsCotasCacheFreshEnough(DashboardSnapshot cached) =>
        cached.Cotas.Value is not null
        && cached.Cotas.StaleAsOf is { } asOf
        && clock.Now - asOf < CotasCacheTtl;

    /// <summary>Falls back to a cached value, marked stale, when the live fetch failed.</summary>
    private SectionResult<T> WithCacheFallback<T>(SectionResult<T> live, SectionResult<T> cached)
        where T : class
    {
        if (live.HasValue || cached.Value is not { } cachedValue)
        {
            return live;
        }

        return SectionResult<T>.Stale(cachedValue, cached.StaleAsOf ?? clock.Now);
    }

    /// <summary>
    /// Attaches each day's sensação térmica and humidity to the matching scraped forecast day.
    /// </summary>
    /// <remarks>
    /// The two sources are independent and can disagree about which dates they cover (the site
    /// publishes 5 days from its own editorial calendar), so the join is by date and an unmatched
    /// day simply keeps its null fields and renders as "indisponível".
    /// </remarks>
    private static SectionResult<IReadOnlyList<DailyForecast>> MergeDailyConditions(
        SectionResult<IReadOnlyList<DailyForecast>> forecast,
        IReadOnlyList<DailyConditions> daily)
    {
        if (!forecast.HasValue || daily.Count == 0)
        {
            return forecast;
        }

        var byDate = daily
            .GroupBy(static d => d.Date)
            .ToDictionary(static g => g.Key, static g => g.First());

        var merged = forecast.Value!
            .Select(day => byDate.TryGetValue(day.Date, out var conditions)
                ? day with { FeelsLikeC = conditions.FeelsLikeC, HumidityPercent = conditions.HumidityPercent }
                : day)
            .ToArray();

        return SectionResult<IReadOnlyList<DailyForecast>>.Ok(merged);
    }

    /// <summary>
    /// Marks the band containing the current reading. The two sources are independent, so the
    /// bands still render unhighlighted when the level reading is the one that failed (and there is
    /// no cached level to fall back to either).
    /// </summary>
    private static SectionResult<IReadOnlyList<RiverThreshold>> HighlightCurrentBand(
        SectionResult<IReadOnlyList<RiverThreshold>> thresholds,
        SectionResult<RiverLevel> river)
    {
        if (!thresholds.HasValue)
        {
            return thresholds;
        }

        var flagged = RiverThreshold.HighlightCurrent(thresholds.Value!, river.Value?.LevelMeters);

        return thresholds.IsStale
            ? SectionResult<IReadOnlyList<RiverThreshold>>.Stale(flagged, thresholds.StaleAsOf!.Value)
            : SectionResult<IReadOnlyList<RiverThreshold>>.Ok(flagged);
    }

    /// <summary>
    /// Merges the three independent sources behind the top card. Each field is optional, so the
    /// card is only reported as failed when nothing at all could be obtained.
    /// </summary>
    private static SectionResult<CurrentWeather> BuildWeather(
        SectionResult<TemperatureReading> temperature,
        SectionResult<ApparentConditions> apparent,
        DayExtremes? extremes)
    {
        var weather = new CurrentWeather
        {
            TemperatureC = temperature.Value?.ValueC,
            ReadingTimeLocal = temperature.Value?.TimeLocal,
            FeelsLikeC = apparent.Value?.FeelsLikeC,
            HumidityPercent = apparent.Value?.HumidityPercent,
            MinC = extremes?.MinC,
            MaxC = extremes?.MaxC,
        };

        return weather.HasAnyValue
            ? SectionResult<CurrentWeather>.Ok(weather)
            : SectionResult<CurrentWeather>.Fail(temperature.Error ?? GenericWeatherError);
    }
}
