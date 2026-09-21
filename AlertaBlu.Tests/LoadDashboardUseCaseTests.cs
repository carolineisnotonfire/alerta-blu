using AlertaBlu.Application;
using AlertaBlu.Domain;
using Xunit;

namespace AlertaBlu.Tests;

/// <summary>
/// Covers the composition and caching logic that used to live inside <c>AlertaBluService</c>:
/// fusing independent sources into the weather/forecast cards, highlighting the current river
/// band, and falling back to the offline cache. Uses a fake gateway throughout, so these are unit
/// tests of pure logic rather than integration tests of HTTP/parsing (that's <see cref="AlertaBluServiceTests"/>'s job).
/// </summary>
public class LoadDashboardUseCaseTests
{
    private static LoadDashboardUseCase CreateUseCase(
        FakeAlertaBluGateway? gateway = null, FakeDashboardCache? cache = null, FakeClock? clock = null) =>
        new(gateway ?? new FakeAlertaBluGateway(), cache ?? new FakeDashboardCache(), clock ?? new FakeClock());

    [Fact]
    public async Task ExecuteAsync_Should_PopulateEverySection_When_GatewayIsHealthy()
    {
        // Act
        var snapshot = await CreateUseCase().ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(snapshot.Weather.HasValue);
        Assert.True(snapshot.Forecast.HasValue);
        Assert.True(snapshot.River.HasValue);
        Assert.True(snapshot.RiverThresholds.HasValue);
        Assert.True(snapshot.Cotas.HasValue);
        Assert.True(snapshot.Barragens.HasValue);
    }

    [Fact]
    public async Task ExecuteAsync_Should_KeepStationTemperature_When_OpenMeteoIsUnavailable()
    {
        // Arrange: the secondary provider is down, but the station feed is fine.
        var gateway = new FakeAlertaBluGateway
        {
            OpenMeteo = new OpenMeteoResult(SectionResult<ApparentConditions>.Fail("erro"), []),
        };

        // Act
        var snapshot = await CreateUseCase(gateway).ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert: losing the secondary provider costs two fields, not the whole card.
        Assert.True(snapshot.Weather.HasValue);
        Assert.Equal(16.04, snapshot.Weather.Value!.TemperatureC!.Value, precision: 2);
        Assert.Null(snapshot.Weather.Value.FeelsLikeC);
        Assert.Null(snapshot.Weather.Value.HumidityPercent);
    }

    [Fact]
    public async Task ExecuteAsync_Should_LoseOnlyExtremesAndForecast_When_DetalhadaIsUnavailable()
    {
        // Arrange: the one page that feeds two different cards.
        var gateway = new FakeAlertaBluGateway
        {
            Detalhada = new DetalhadaResult(SectionResult<IReadOnlyList<DailyForecast>>.Fail("erro"), null),
        };

        // Act
        var snapshot = await CreateUseCase(gateway).ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(snapshot.Forecast.HasError);
        Assert.True(snapshot.Weather.HasValue);
        Assert.Equal(16.04, snapshot.Weather.Value!.TemperatureC!.Value, precision: 2);
        Assert.Null(snapshot.Weather.Value.MinC);
        Assert.Null(snapshot.Weather.Value.MaxC);
    }

    [Fact]
    public async Task ExecuteAsync_Should_ReportWeatherError_When_NoWeatherSourceResponds()
    {
        // Arrange
        var gateway = new FakeAlertaBluGateway
        {
            Temperature = SectionResult<TemperatureReading>.Fail("sem estação"),
            OpenMeteo = new OpenMeteoResult(SectionResult<ApparentConditions>.Fail("erro"), []),
            Detalhada = new DetalhadaResult(SectionResult<IReadOnlyList<DailyForecast>>.Fail("erro"), null),
        };

        // Act
        var snapshot = await CreateUseCase(gateway).ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(snapshot.Weather.HasValue);
        Assert.True(snapshot.Weather.HasError);

        // Unrelated sections are untouched.
        Assert.True(snapshot.River.HasValue);
        Assert.True(snapshot.Cotas.HasValue);
    }

    [Fact]
    public async Task ExecuteAsync_Should_AttachFeelsLikeAndHumidity_ToMatchingForecastDay()
    {
        // Arrange
        var gateway = new FakeAlertaBluGateway
        {
            OpenMeteo = new OpenMeteoResult(
                SectionResult<ApparentConditions>.Ok(new ApparentConditions(16.9, 93)),
                [new DailyConditions(new DateOnly(2026, 8, 13), 23.5, 70)]),
        };

        // Act
        var snapshot = await CreateUseCase(gateway).ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert: the Open-Meteo "daily" block is joined onto the scraped forecast by date.
        var thursday = snapshot.Forecast.Value!.Single(f => f.Date == new DateOnly(2026, 8, 13));
        Assert.Equal(23.5, thursday.FeelsLikeC!.Value, precision: 2);
        Assert.Equal(70, thursday.HumidityPercent);
    }

    [Fact]
    public async Task ExecuteAsync_Should_HighlightBand_ContainingTheCurrentRiverLevel()
    {
        // Act: river level 2,25m falls in the 0-3,0m "Normalidade" band.
        var snapshot = await CreateUseCase().ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(snapshot.RiverThresholds.Value![0].IsCurrent);
        Assert.Equal("Normalidade", snapshot.RiverThresholds.Value[0].Label);
    }

    #region Offline cache

    [Fact]
    public async Task ExecuteAsync_Should_ServeStaleCache_When_LiveFetchFails()
    {
        // Arrange: yesterday's reading is all that is available for the river card.
        var clock = new FakeClock();
        var staleAsOf = clock.Now.AddHours(-6);
        var cache = new FakeDashboardCache
        {
            Seeded = new DashboardSnapshot
            {
                River = SectionResult<RiverLevel>.Stale(new RiverLevel { LevelMeters = 1.90 }, staleAsOf),
            },
        };
        var gateway = new FakeAlertaBluGateway { River = SectionResult<RiverLevel>.Fail("erro") };

        // Act
        var snapshot = await CreateUseCase(gateway, cache, clock).ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert: the section still renders, flagged stale, rather than showing an error.
        Assert.True(snapshot.River.HasValue);
        Assert.False(snapshot.River.HasError);
        Assert.True(snapshot.River.IsStale);
        Assert.Equal(1.90, snapshot.River.Value!.LevelMeters, precision: 2);
        Assert.Equal(staleAsOf, snapshot.River.StaleAsOf);
    }

    [Fact]
    public async Task ExecuteAsync_Should_PreferLiveData_Over_StaleCache_When_LiveFetchSucceeds()
    {
        // Arrange
        var clock = new FakeClock();
        var cache = new FakeDashboardCache
        {
            Seeded = new DashboardSnapshot
            {
                River = SectionResult<RiverLevel>.Stale(new RiverLevel { LevelMeters = 0.10 }, clock.Now.AddDays(-1)),
            },
        };

        // Act
        var snapshot = await CreateUseCase(cache: cache, clock: clock)
            .ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(snapshot.River.IsStale);
        Assert.Equal(2.25, snapshot.River.Value!.LevelMeters, precision: 2);
    }

    [Fact]
    public async Task ExecuteAsync_Should_ReportError_When_LiveFetchFails_And_NoCacheExists()
    {
        // Arrange: first-ever launch, offline.
        var gateway = new FakeAlertaBluGateway { River = SectionResult<RiverLevel>.Fail("erro") };

        // Act
        var snapshot = await CreateUseCase(gateway).ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(snapshot.River.HasValue);
        Assert.True(snapshot.River.HasError);
        Assert.False(snapshot.River.IsStale);
    }

    [Fact]
    public async Task ExecuteAsync_Should_SkipCotasNetworkFetch_When_CachedCopyIsWithinTtl()
    {
        // Arrange
        var clock = new FakeClock();
        var gateway = new FakeAlertaBluGateway();
        var cache = new FakeDashboardCache
        {
            Seeded = new DashboardSnapshot
            {
                Cotas = SectionResult<IReadOnlyList<CotaEnchente>>.Stale(
                    [new CotaEnchente { Logradouro = "Rua Cache" }], clock.Now.AddHours(-1)),
            },
        };

        // Act
        var snapshot = await CreateUseCase(gateway, cache, clock).ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert: served from cache, not re-fetched, and not flagged stale (the skip is
        // deliberate policy, not a fetch failure).
        Assert.Equal(0, gateway.CotasCallCount);
        Assert.False(snapshot.Cotas.IsStale);
        Assert.Equal("Rua Cache", snapshot.Cotas.Value!.Single().Logradouro);
    }

    [Fact]
    public async Task ExecuteAsync_Should_FetchCotas_When_CachedCopyIsOlderThanTtl()
    {
        // Arrange
        var clock = new FakeClock();
        var gateway = new FakeAlertaBluGateway();
        var cache = new FakeDashboardCache
        {
            Seeded = new DashboardSnapshot
            {
                Cotas = SectionResult<IReadOnlyList<CotaEnchente>>.Stale(
                    [new CotaEnchente { Logradouro = "Rua Velha" }], clock.Now.AddHours(-25)),
            },
        };

        // Act
        await CreateUseCase(gateway, cache, clock).ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, gateway.CotasCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_Should_WriteTheMergedSnapshot_BackToCache_StampedWithTheClock()
    {
        // Arrange
        var clock = new FakeClock();
        var cache = new FakeDashboardCache();

        // Act
        await CreateUseCase(cache: cache, clock: clock).ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(cache.LastWritten);
        Assert.True(cache.LastWritten.Weather.HasValue);
        Assert.True(cache.LastWritten.River.HasValue);
        Assert.Equal(clock.Now, cache.LastWritten.LoadedAt);
    }

    #endregion
}
