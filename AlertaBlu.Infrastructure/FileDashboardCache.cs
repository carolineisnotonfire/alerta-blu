using System.Text.Json;
using AlertaBlu.Application;
using AlertaBlu.Domain;
using Microsoft.Extensions.Logging;

namespace AlertaBlu.Infrastructure;

/// <summary>
/// File-backed <see cref="IDashboardCache"/>. Deliberately free of any platform storage API (this
/// project stays a plain net10.0 library): the caller supplies the file path, so the MAUI head can
/// point it at <c>FileSystem.AppDataDirectory</c> while tests point it at a temp directory.
/// </summary>
public sealed class FileDashboardCache(string cacheFilePath, ILogger<FileDashboardCache> logger)
    : IDashboardCache
{
    private readonly ILogger<FileDashboardCache> _logger = logger;

    public async Task<DashboardSnapshot?> ReadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(cacheFilePath))
            {
                return null;
            }

            await using var stream = File.OpenRead(cacheFilePath);
            var dto = await JsonSerializer
                .DeserializeAsync(stream, AlertaBluJsonContext.Default.CachedDashboardDto, cancellationToken)
                .ConfigureAwait(false);

            return dto is null ? null : ToSnapshot(dto);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "Falha ao ler o cache do painel em {Path}.", cacheFilePath);
            return null;
        }
    }

    public async Task WriteAsync(DashboardSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        try
        {
            var directory = Path.GetDirectoryName(cacheFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = File.Create(cacheFilePath);
            await JsonSerializer
                .SerializeAsync(stream, ToDto(snapshot), AlertaBluJsonContext.Default.CachedDashboardDto, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Falha ao gravar o cache do painel em {Path}.", cacheFilePath);
        }
    }

    private static DashboardSnapshot ToSnapshot(CachedDashboardDto dto) => new()
    {
        Weather = ToSection<CurrentWeather>(dto.Weather, dto.WeatherAsOf),
        Forecast = ToSection<IReadOnlyList<DailyForecast>>(dto.Forecast, dto.ForecastAsOf),
        River = ToSection<RiverLevel>(dto.River, dto.RiverAsOf),
        RiverThresholds = ToSection<IReadOnlyList<RiverThreshold>>(dto.RiverThresholds, dto.RiverThresholdsAsOf),
        Cotas = ToSection<IReadOnlyList<CotaEnchente>>(dto.Cotas, dto.CotasAsOf),
        Barragens = ToSection<IReadOnlyList<Barragem>>(dto.Barragens, dto.BarragensAsOf),
    };

    private static SectionResult<T> ToSection<T>(T? value, DateTimeOffset? asOf)
        where T : class =>
        value is not null && asOf is { } stamp ? SectionResult<T>.Stale(value, stamp) : SectionResult<T>.Empty;

    private static CachedDashboardDto ToDto(DashboardSnapshot snapshot) => new()
    {
        Weather = snapshot.Weather.Value,
        WeatherAsOf = AsOf(snapshot.Weather, snapshot.LoadedAt),
        Forecast = snapshot.Forecast.Value?.ToArray(),
        ForecastAsOf = AsOf(snapshot.Forecast, snapshot.LoadedAt),
        River = snapshot.River.Value,
        RiverAsOf = AsOf(snapshot.River, snapshot.LoadedAt),
        RiverThresholds = snapshot.RiverThresholds.Value?.ToArray(),
        RiverThresholdsAsOf = AsOf(snapshot.RiverThresholds, snapshot.LoadedAt),
        Cotas = snapshot.Cotas.Value?.ToArray(),
        CotasAsOf = AsOf(snapshot.Cotas, snapshot.LoadedAt),
        Barragens = snapshot.Barragens.Value?.ToArray(),
        BarragensAsOf = AsOf(snapshot.Barragens, snapshot.LoadedAt),
    };

    /// <summary>
    /// An already-stale section keeps the timestamp of when it was last genuinely fresh, rather
    /// than being stamped "now" every time a failed refresh re-persists it unchanged.
    /// </summary>
    private static DateTimeOffset? AsOf<T>(SectionResult<T> section, DateTimeOffset loadedAt)
        where T : class =>
        section.StaleAsOf ?? (section.HasValue ? loadedAt : null);
}
