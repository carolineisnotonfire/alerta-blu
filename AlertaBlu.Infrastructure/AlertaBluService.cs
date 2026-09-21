using System.Net;
using AlertaBlu.Application;
using AlertaBlu.Domain;
using Microsoft.Extensions.Logging;

namespace AlertaBlu.Infrastructure;

/// <summary>
/// Fetches and parses the AlertaBLU/Open-Meteo sources, one method per dashboard section. Pure
/// I/O: no cross-section composition, no caching, no fusing of independent sources into one card —
/// that is <see cref="LoadDashboardUseCase"/>'s job, upstream in the Application layer. Every
/// endpoint owns its own failure handling, so one being down, slow or reshaped degrades exactly one
/// section instead of the whole screen.
/// </summary>
public sealed class AlertaBluService(HttpClient httpClient, AlertaBluOptions options, ILogger<AlertaBluService> logger)
    : IAlertaBluGateway
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly AlertaBluOptions _options = options;
    private readonly ILogger<AlertaBluService> _logger = logger;

    public Task<SectionResult<TemperatureReading>> GetTemperatureAsync(CancellationToken cancellationToken = default) =>
        LoadAsync(_options.TemperaturasUrl, AlertaBluParser.ParseLatestTemperature, "temperatura", cancellationToken);

    public Task<SectionResult<RiverLevel>> GetRiverLevelAsync(CancellationToken cancellationToken = default) =>
        LoadAsync(_options.NivelDoRioUrl, AlertaBluParser.ParseRiverLevel, "nível do rio", cancellationToken);

    public Task<SectionResult<IReadOnlyList<RiverThreshold>>> GetRiverThresholdsAsync(
        CancellationToken cancellationToken = default) =>
        LoadAsync(_options.NivelOficialUrl, AlertaBluParser.ParseRiverThresholds, "cotas oficiais", cancellationToken);

    public Task<SectionResult<IReadOnlyList<CotaEnchente>>> GetCotasAsync(CancellationToken cancellationToken = default) =>
        LoadAsync(_options.CotasUrl, AlertaBluParser.ParseCotas, "cotas", cancellationToken);

    public Task<SectionResult<IReadOnlyList<Barragem>>> GetBarragensAsync(CancellationToken cancellationToken = default) =>
        LoadAsync(_options.BarragensUrl, AlertaBluParser.ParseBarragens, "barragens", cancellationToken);

    /// <summary>
    /// Splits the single "detalhada" download into the two cards it feeds. Today's extremes are
    /// best-effort: losing them must not cost the 5-day strip, or vice versa.
    /// </summary>
    public async Task<DetalhadaResult> GetDetalhadaAsync(CancellationToken cancellationToken = default)
    {
        var detalhada = await LoadAsync(
            _options.DetalhadaUrl, static html => html, "previsão", cancellationToken)
            .ConfigureAwait(false);

        if (!detalhada.HasValue)
        {
            return new DetalhadaResult(
                SectionResult<IReadOnlyList<DailyForecast>>.Fail(detalhada.Error ?? Messages.Generic), null);
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

        return new DetalhadaResult(forecast, extremes);
    }

    /// <summary>
    /// Splits the single Open-Meteo download into today's conditions and the per-day figures.
    /// Both are best-effort: the daily block is a decorative addition to the forecast chips and
    /// must never cost the hero card's sensação térmica, or vice versa.
    /// </summary>
    public async Task<OpenMeteoResult> GetOpenMeteoAsync(CancellationToken cancellationToken = default)
    {
        var openMeteo = await LoadAsync(
            _options.OpenMeteoUrl, static json => json, "sensação térmica", cancellationToken)
            .ConfigureAwait(false);

        if (!openMeteo.HasValue)
        {
            return new OpenMeteoResult(SectionResult<ApparentConditions>.Fail(openMeteo.Error ?? Messages.Generic), []);
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

        return new OpenMeteoResult(apparent, daily);
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
