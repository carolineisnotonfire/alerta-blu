using System.Net;
using AlertaBlu.Application;
using AlertaBlu.Domain;
using Microsoft.Extensions.Logging;

namespace AlertaBlu.Infrastructure;

/// <summary>
/// Loads the dashboard from the Civil Defense site.
/// </summary>
/// <remarks>
/// All seven requests are issued concurrently and each one owns its failure handling, so a single
/// endpoint being down, slow or reshaped degrades exactly one card instead of the whole screen.
/// Two of them feed more than one card and are therefore fetched once and parsed twice: the
/// "detalhada" page (today's min/max and the 5-day strip) and the Open-Meteo response (today's
/// sensação térmica and the per-day figures behind the forecast chips).
/// </remarks>
public sealed class AlertaBluService(HttpClient httpClient, IDashboardCache cache, ILogger<AlertaBluService> logger)
    : IAlertaBluService
{
    /// <summary>
    /// Cotas rarely changes (street flood thresholds are edited maybe yearly) and is by far the
    /// heaviest download (~700 KB), so a cached copy this fresh is served without hitting the
    /// network at all.
    /// </summary>
    private static readonly TimeSpan CotasCacheTtl = TimeSpan.FromHours(24);

    private readonly HttpClient _httpClient = httpClient;
    private readonly IDashboardCache _cache = cache;
    private readonly ILogger<AlertaBluService> _logger = logger;

    public async Task<DashboardSnapshot> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var cached = await _cache.ReadAsync(cancellationToken).ConfigureAwait(false) ?? new DashboardSnapshot();

        var temperatureTask = LoadAsync(
            AlertaBluEndpoints.Temperaturas, AlertaBluParser.ParseLatestTemperature, "temperatura", cancellationToken);
        var detalhadaTask = LoadAsync(
            AlertaBluEndpoints.Detalhada, static html => html, "previsão", cancellationToken);
        var openMeteoTask = LoadAsync(
            AlertaBluEndpoints.OpenMeteo, static json => json, "sensação térmica", cancellationToken);
        var riverTask = LoadAsync(
            AlertaBluEndpoints.NivelDoRio, AlertaBluParser.ParseRiverLevel, "nível do rio", cancellationToken);
        var thresholdsTask = LoadAsync(
            AlertaBluEndpoints.NivelOficial, AlertaBluParser.ParseRiverThresholds, "cotas oficiais", cancellationToken);
        var cotasTask = IsCotasCacheFreshEnough(cached)
            ? Task.FromResult(SectionResult<IReadOnlyList<CotaEnchente>>.Ok(cached.Cotas.Value!))
            : LoadAsync(AlertaBluEndpoints.Cotas, AlertaBluParser.ParseCotas, "cotas", cancellationToken);
        var barragensTask = LoadAsync(
            AlertaBluEndpoints.Barragens, AlertaBluParser.ParseBarragens, "barragens", cancellationToken);

        await Task.WhenAll(
            temperatureTask, detalhadaTask, openMeteoTask,
            riverTask, thresholdsTask, cotasTask, barragensTask)
            .ConfigureAwait(false);

        var temperature = await temperatureTask.ConfigureAwait(false);
        var detalhada = await detalhadaTask.ConfigureAwait(false);
        var openMeteo = await openMeteoTask.ConfigureAwait(false);

        var (forecast, extremes) = ParseDetalhada(detalhada);
        var (apparent, dailyConditions) = ParseOpenMeteo(openMeteo);

        // The stale-cache fallback for a failed live fetch, so one dropped request degrades to
        // "showing data from HH:mm" rather than a blank section.
        var river = WithCacheFallback(await riverTask.ConfigureAwait(false), cached.River);

        var snapshot = new DashboardSnapshot
        {
            Weather = WithCacheFallback(BuildWeather(temperature, apparent, extremes), cached.Weather),
            Forecast = WithCacheFallback(MergeDailyConditions(forecast, dailyConditions), cached.Forecast),
            River = river,
            RiverThresholds = WithCacheFallback(
                HighlightCurrentBand(await thresholdsTask.ConfigureAwait(false), river), cached.RiverThresholds),
            Cotas = WithCacheFallback(await cotasTask.ConfigureAwait(false), cached.Cotas),
            Barragens = WithCacheFallback(await barragensTask.ConfigureAwait(false), cached.Barragens),
        };

        await _cache.WriteAsync(snapshot, cancellationToken).ConfigureAwait(false);

        return snapshot;
    }

    private static bool IsCotasCacheFreshEnough(DashboardSnapshot cached) =>
        cached.Cotas.Value is not null
        && cached.Cotas.StaleAsOf is { } asOf
        && DateTimeOffset.Now - asOf < CotasCacheTtl;

    /// <summary>Falls back to a cached value, marked stale, when the live fetch failed.</summary>
    private static SectionResult<T> WithCacheFallback<T>(SectionResult<T> live, SectionResult<T> cached)
        where T : class
    {
        if (live.HasValue || cached.Value is not { } cachedValue)
        {
            return live;
        }

        return SectionResult<T>.Stale(cachedValue, cached.StaleAsOf ?? DateTimeOffset.Now);
    }

    /// <summary>
    /// Splits the single "detalhada" download into the two cards it feeds. Today's extremes are
    /// best-effort: losing them must not cost us the 5-day strip, or vice versa.
    /// </summary>
    private (SectionResult<IReadOnlyList<DailyForecast>> Forecast, DayExtremes? Extremes) ParseDetalhada(
        SectionResult<string> detalhada)
    {
        if (!detalhada.HasValue)
        {
            return (SectionResult<IReadOnlyList<DailyForecast>>.Fail(detalhada.Error ?? Messages.Generic), null);
        }

        var html = detalhada.Value!;

        SectionResult<IReadOnlyList<DailyForecast>> forecast;
        try
        {
            forecast = SectionResult<IReadOnlyList<DailyForecast>>.Ok(AlertaBluParser.ParseForecast(html));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao interpretar a previsão de 5 dias.");
            forecast = SectionResult<IReadOnlyList<DailyForecast>>.Fail(Messages.Parse("previsão"));
        }

        DayExtremes? extremes;
        try
        {
            extremes = AlertaBluParser.ParseDayExtremes(html);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao interpretar mínima/máxima do dia.");
            extremes = null;
        }

        return (forecast, extremes);
    }

    /// <summary>
    /// Splits the single Open-Meteo download into today's conditions and the per-day figures.
    /// Both are best-effort: the daily block is a decorative addition to the forecast chips and
    /// must never cost us the hero card's sensação térmica, or vice versa.
    /// </summary>
    private (SectionResult<ApparentConditions> Apparent, IReadOnlyList<DailyConditions> Daily) ParseOpenMeteo(
        SectionResult<string> openMeteo)
    {
        if (!openMeteo.HasValue)
        {
            return (SectionResult<ApparentConditions>.Fail(openMeteo.Error ?? Messages.Generic), []);
        }

        var json = openMeteo.Value!;

        SectionResult<ApparentConditions> apparent;
        try
        {
            apparent = SectionResult<ApparentConditions>.Ok(AlertaBluParser.ParseApparentConditions(json));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao interpretar as condições atuais do Open-Meteo.");
            apparent = SectionResult<ApparentConditions>.Fail(Messages.Parse("sensação térmica"));
        }

        IReadOnlyList<DailyConditions> daily;
        try
        {
            daily = AlertaBluParser.ParseDailyConditions(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao interpretar a previsão diária do Open-Meteo.");
            daily = [];
        }

        return (apparent, daily);
    }

    /// <summary>
    /// Attaches each day's sensação térmica and humidity to the matching scraped forecast.
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
    /// bands still render unhighlighted when the level scrape is the one that failed.
    /// </summary>
    private static SectionResult<IReadOnlyList<RiverThreshold>> HighlightCurrentBand(
        SectionResult<IReadOnlyList<RiverThreshold>> thresholds,
        SectionResult<RiverLevel> river)
    {
        if (!thresholds.HasValue)
        {
            return thresholds;
        }

        return SectionResult<IReadOnlyList<RiverThreshold>>.Ok(
            AlertaBluParser.HighlightCurrent(thresholds.Value!, river.Value?.LevelMeters));
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
            : SectionResult<CurrentWeather>.Fail(temperature.Error ?? Messages.Generic);
    }

    /// <summary>
    /// Downloads one endpoint and runs its parser, converting any upstream failure into a
    /// displayable message. Cancellation is the only exception allowed to escape.
    /// </summary>
    private async Task<SectionResult<T>> LoadAsync<T>(
        string url,
        Func<string, T> parse,
        string section,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            var body = await _httpClient.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            return SectionResult<T>.Ok(parse(body));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            // Not user-initiated: HttpClient surfaces its own timeout as TaskCanceledException.
            _logger.LogWarning(ex, "Tempo esgotado ao carregar {Section} ({Url}).", section, url);
            return SectionResult<T>.Fail(Messages.Timeout(section));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Falha de rede ao carregar {Section} ({Url}).", section, url);
            return SectionResult<T>.Fail(
                ex.StatusCode is { } status
                    ? Messages.Http(section, status)
                    : Messages.Network(section));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao interpretar {Section} ({Url}).", section, url);
            return SectionResult<T>.Fail(Messages.Parse(section));
        }
    }

    private static class Messages
    {
        public const string Generic = "Não foi possível carregar os dados.";

        public static string Network(string section) =>
            $"Sem conexão para carregar {section}.";

        public static string Timeout(string section) =>
            $"Tempo esgotado ao carregar {section}.";

        public static string Http(string section, HttpStatusCode status) =>
            $"O site respondeu {(int)status} ao carregar {section}.";

        public static string Parse(string section) =>
            $"Não foi possível interpretar os dados de {section}.";
    }
}
