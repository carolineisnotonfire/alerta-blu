using AlertaBlu.Domain;
using AlertaBlu.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AlertaBlu.Tests;

/// <summary>
/// Round-trips <see cref="DashboardSnapshot"/> through the on-disk cache. A temp file per test
/// keeps these independent of any real device path and of each other.
/// </summary>
public sealed class FileDashboardCacheTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"alertablu-cache-test-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    private FileDashboardCache CreateCache() => new(_path, NullLogger<FileDashboardCache>.Instance);

    [Fact]
    public async Task ReadAsync_Should_ReturnNull_When_NoFileHasBeenWritten()
    {
        // Act
        var snapshot = await CreateCache().ReadAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(snapshot);
    }

    [Fact]
    public async Task ReadAsync_Should_ReturnNull_When_TheFileIsNotValidJson()
    {
        // Arrange
        await File.WriteAllTextAsync(_path, "{ not json", TestContext.Current.CancellationToken);

        // Act
        var snapshot = await CreateCache().ReadAsync(TestContext.Current.CancellationToken);

        // Assert: a corrupt cache degrades to "no cache", never an exception.
        Assert.Null(snapshot);
    }

    [Fact]
    public async Task WriteAsync_Then_ReadAsync_Should_RoundTrip_EveryFreshSection_As_Stale()
    {
        // Arrange
        var cache = CreateCache();
        var loadedAt = new DateTimeOffset(2026, 8, 13, 8, 0, 0, TimeSpan.FromHours(-3));
        var original = new DashboardSnapshot
        {
            Weather = SectionResult<CurrentWeather>.Ok(new CurrentWeather { TemperatureC = 16.04 }),
            Forecast = SectionResult<IReadOnlyList<DailyForecast>>.Ok(
                [new DailyForecast { Date = new DateOnly(2026, 8, 13) }]),
            River = SectionResult<RiverLevel>.Ok(new RiverLevel { LevelMeters = 2.25 }),
            RiverThresholds = SectionResult<IReadOnlyList<RiverThreshold>>.Ok(
                [new RiverThreshold { Label = "Normalidade", MinMeters = 0 }]),
            Cotas = SectionResult<IReadOnlyList<CotaEnchente>>.Ok(
                [new CotaEnchente { Logradouro = "Rua Sao Rafael", CotaMeters = 7.40 }]),
            Barragens = SectionResult<IReadOnlyList<Barragem>>.Ok(
                [new Barragem { Estacao = "Barragem Oeste Taió", CapacityPercent = 2.90 }]),
            LoadedAt = loadedAt,
        };

        // Act
        await cache.WriteAsync(original, TestContext.Current.CancellationToken);
        var restored = await cache.ReadAsync(TestContext.Current.CancellationToken);

        // Assert: every section survives the round trip, now stamped as of the write.
        Assert.NotNull(restored);
        Assert.Equal(16.04, restored.Weather.Value!.TemperatureC!.Value, precision: 2);
        Assert.Equal(loadedAt, restored.Weather.StaleAsOf);

        Assert.Equal(2.25, restored.River.Value!.LevelMeters, precision: 2);
        Assert.Equal("Rua Sao Rafael", restored.Cotas.Value!.Single().Logradouro);
        Assert.Equal("Barragem Oeste Taió", restored.Barragens.Value!.Single().Estacao);
        Assert.Equal("Normalidade", restored.RiverThresholds.Value!.Single().Label);
        Assert.Single(restored.Forecast.Value!);
    }

    [Fact]
    public async Task WriteAsync_Then_ReadAsync_Should_PreserveOriginalStaleTimestamp_Not_TheWriteTime()
    {
        // Arrange: a section that was already stale when this snapshot was assembled (a repeat
        // failure re-persisting the same old value) must not have its clock reset to "now".
        var cache = CreateCache();
        var originalStaleAsOf = DateTimeOffset.Now.AddDays(-2);
        var snapshot = new DashboardSnapshot
        {
            River = SectionResult<RiverLevel>.Stale(new RiverLevel { LevelMeters = 1.5 }, originalStaleAsOf),
        };

        // Act
        await cache.WriteAsync(snapshot, TestContext.Current.CancellationToken);
        var restored = await cache.ReadAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(originalStaleAsOf, restored!.River.StaleAsOf);
    }

    [Fact]
    public async Task ReadAsync_Should_LeaveSectionEmpty_When_ItWasNeverSuccessfullyLoaded()
    {
        // Arrange: a snapshot where the cotas section failed outright (no value to persist).
        var cache = CreateCache();
        var snapshot = new DashboardSnapshot { Cotas = SectionResult<IReadOnlyList<CotaEnchente>>.Fail("erro") };

        // Act
        await cache.WriteAsync(snapshot, TestContext.Current.CancellationToken);
        var restored = await cache.ReadAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(restored!.Cotas.HasValue);
        Assert.False(restored.Cotas.IsStale);
    }
}
