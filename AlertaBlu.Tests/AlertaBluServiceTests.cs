using AlertaBlu.Application;
using AlertaBlu.Domain;
using AlertaBlu.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AlertaBlu.Tests;

/// <summary>
/// Covers the property that matters most for a screen scraped from a live third-party site:
/// each section fails on its own and never takes the others down with it.
/// </summary>
public class AlertaBluServiceTests
{
    private static AlertaBluService CreateService(StubHttpMessageHandler handler, IDashboardCache? cache = null) =>
        new(new HttpClient(handler), cache ?? new FakeDashboardCache(), NullLogger<AlertaBluService>.Instance);

    [Fact]
    public async Task GetDashboardAsync_Should_PopulateEverySection_When_AllSourcesAreHealthy()
    {
        // Arrange
        var service = CreateService(StubHttpMessageHandler.Healthy());

        // Act
        var snapshot = await service.GetDashboardAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(snapshot.Weather.HasValue);
        Assert.True(snapshot.Forecast.HasValue);
        Assert.True(snapshot.River.HasValue);
        Assert.True(snapshot.RiverThresholds.HasValue);
        Assert.True(snapshot.Cotas.HasValue);
        Assert.True(snapshot.Barragens.HasValue);

        Assert.Equal(16.04, snapshot.Weather.Value!.TemperatureC!.Value, precision: 2);
        Assert.Equal(93, snapshot.Weather.Value.HumidityPercent);
        Assert.Equal(14d, snapshot.Weather.Value.MinC);
        Assert.Equal(5, snapshot.Forecast.Value!.Count);
        Assert.Equal(3, snapshot.Barragens.Value!.Count);

        // River level is 2,25m (RiverHtml), which falls in the lowest band (0 – 3,0m).
        Assert.Equal(3, snapshot.RiverThresholds.Value!.Count);
        Assert.True(snapshot.RiverThresholds.Value[0].IsCurrent);
        Assert.Equal("Normalidade", snapshot.RiverThresholds.Value[0].Label);
    }

    [Fact]
    public async Task GetDashboardAsync_Should_AttachFeelsLikeAndHumidity_ToMatchingForecastDay()
    {
        // Arrange
        var service = CreateService(StubHttpMessageHandler.Healthy());

        // Act
        var snapshot = await service.GetDashboardAsync(TestContext.Current.CancellationToken);

        // Assert: the Open-Meteo "daily" block is joined onto the scraped forecast by date.
        var thursday = snapshot.Forecast.Value!.Single(f => f.Date == new DateOnly(2026, 8, 13));
        Assert.Equal(23.5, thursday.FeelsLikeC!.Value, precision: 2);
        Assert.Equal(70, thursday.HumidityPercent);
    }

    [Fact]
    public async Task GetDashboardAsync_Should_KeepOtherSections_When_OneEndpointFails()
    {
        // Arrange: the cotas page is down.
        var service = CreateService(StubHttpMessageHandler.Healthy().Without("/p/cotas"));

        // Act
        var snapshot = await service.GetDashboardAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(snapshot.Cotas.HasValue);
        Assert.True(snapshot.Cotas.HasError);
        Assert.Contains("503", snapshot.Cotas.Error!, StringComparison.Ordinal);

        Assert.True(snapshot.Weather.HasValue);
        Assert.True(snapshot.Forecast.HasValue);
        Assert.True(snapshot.River.HasValue);
        Assert.True(snapshot.Barragens.HasValue);
    }

    [Fact]
    public async Task GetDashboardAsync_Should_KeepStationTemperature_When_OpenMeteoIsUnavailable()
    {
        // Arrange
        var service = CreateService(StubHttpMessageHandler.Healthy().Without("api.open-meteo.com"));

        // Act
        var snapshot = await service.GetDashboardAsync(TestContext.Current.CancellationToken);

        // Assert: losing the secondary provider costs two fields, not the whole card.
        Assert.True(snapshot.Weather.HasValue);
        Assert.Equal(16.04, snapshot.Weather.Value!.TemperatureC!.Value, precision: 2);
        Assert.Null(snapshot.Weather.Value.FeelsLikeC);
        Assert.Null(snapshot.Weather.Value.HumidityPercent);
    }

    [Fact]
    public async Task GetDashboardAsync_Should_LoseOnlyExtremesAndForecast_When_DetalhadaIsUnavailable()
    {
        // Arrange: the one page that feeds two different cards.
        var service = CreateService(StubHttpMessageHandler.Healthy().Without("/p/detalhada"));

        // Act
        var snapshot = await service.GetDashboardAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(snapshot.Forecast.HasError);
        Assert.True(snapshot.Weather.HasValue);
        Assert.Equal(16.04, snapshot.Weather.Value!.TemperatureC!.Value, precision: 2);
        Assert.Null(snapshot.Weather.Value.MinC);
        Assert.Null(snapshot.Weather.Value.MaxC);
    }

    [Fact]
    public async Task GetDashboardAsync_Should_ReportWeatherError_When_NoWeatherSourceResponds()
    {
        // Arrange
        var service = CreateService(
            StubHttpMessageHandler.Healthy()
                .Without("temperaturas.json")
                .Without("api.open-meteo.com")
                .Without("/p/detalhada"));

        // Act
        var snapshot = await service.GetDashboardAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(snapshot.Weather.HasValue);
        Assert.True(snapshot.Weather.HasError);

        // Unrelated sections are untouched.
        Assert.True(snapshot.River.HasValue);
        Assert.True(snapshot.Cotas.HasValue);
    }

    [Fact]
    public async Task GetDashboardAsync_Should_ReportParseError_When_PayloadShapeChanges()
    {
        // Arrange: the endpoint answers 200 with markup the parser cannot read.
        var service = CreateService(
            StubHttpMessageHandler.Healthy().Respond("/d/barragens", "<html><body>redesenhado</body></html>"));

        // Act
        var snapshot = await service.GetDashboardAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(snapshot.Barragens.HasError);
        Assert.True(snapshot.River.HasValue);
    }

    [Fact]
    public async Task GetDashboardAsync_Should_FetchDetalhadaOnce_Even_ThoughItFeedsTwoCards()
    {
        // Arrange
        var handler = StubHttpMessageHandler.Healthy();
        var service = CreateService(handler);

        // Act
        await service.GetDashboardAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, handler.RequestedUrls.Count(url => url.Contains("/p/detalhada", StringComparison.Ordinal)));
        Assert.Equal(7, handler.RequestedUrls.Count);
    }

    [Fact]
    public async Task GetDashboardAsync_Should_PropagateCancellation()
    {
        // Arrange
        var service = CreateService(new StubHttpMessageHandler { BlockUntilCancelled = true });
        using var cancellation = new CancellationTokenSource();

        // Act
        var pending = service.GetDashboardAsync(cancellation.Token);
        await cancellation.CancelAsync();

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    #region Offline cache

    [Fact]
    public async Task GetDashboardAsync_Should_ServeStaleCache_When_LiveFetchFails()
    {
        // Arrange: yesterday's reading is all that is available for the river card.
        var staleAsOf = DateTimeOffset.Now.AddHours(-6);
        var cache = new FakeDashboardCache
        {
            Seeded = new DashboardSnapshot
            {
                River = SectionResult<RiverLevel>.Stale(new RiverLevel { LevelMeters = 1.90 }, staleAsOf),
            },
        };
        var service = CreateService(StubHttpMessageHandler.Healthy().Without("/d/nivel-do-rio"), cache);

        // Act
        var snapshot = await service.GetDashboardAsync(TestContext.Current.CancellationToken);

        // Assert: the section still renders, flagged stale, rather than showing an error.
        Assert.True(snapshot.River.HasValue);
        Assert.False(snapshot.River.HasError);
        Assert.True(snapshot.River.IsStale);
        Assert.Equal(1.90, snapshot.River.Value!.LevelMeters, precision: 2);
        Assert.Equal(staleAsOf, snapshot.River.StaleAsOf);
    }

    [Fact]
    public async Task GetDashboardAsync_Should_PreferLiveData_Over_StaleCache_When_LiveFetchSucceeds()
    {
        // Arrange
        var cache = new FakeDashboardCache
        {
            Seeded = new DashboardSnapshot
            {
                River = SectionResult<RiverLevel>.Stale(
                    new RiverLevel { LevelMeters = 0.10 }, DateTimeOffset.Now.AddDays(-1)),
            },
        };
        var service = CreateService(StubHttpMessageHandler.Healthy(), cache);

        // Act
        var snapshot = await service.GetDashboardAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(snapshot.River.IsStale);
        Assert.Equal(2.25, snapshot.River.Value!.LevelMeters, precision: 2);
    }

    [Fact]
    public async Task GetDashboardAsync_Should_ReportError_When_LiveFetchFails_And_NoCacheExists()
    {
        // Arrange: first-ever launch, offline. Unchanged from the pre-cache behaviour.
        var service = CreateService(StubHttpMessageHandler.Healthy().Without("/d/nivel-do-rio"));

        // Act
        var snapshot = await service.GetDashboardAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(snapshot.River.HasValue);
        Assert.True(snapshot.River.HasError);
        Assert.False(snapshot.River.IsStale);
    }

    [Fact]
    public async Task GetDashboardAsync_Should_SkipCotasNetworkFetch_When_CachedCopyIsWithinTtl()
    {
        // Arrange
        var handler = StubHttpMessageHandler.Healthy();
        var cache = new FakeDashboardCache
        {
            Seeded = new DashboardSnapshot
            {
                Cotas = SectionResult<IReadOnlyList<CotaEnchente>>.Stale(
                    [new CotaEnchente { Logradouro = "Rua Cache" }], DateTimeOffset.Now.AddHours(-1)),
            },
        };
        var service = CreateService(handler, cache);

        // Act
        var snapshot = await service.GetDashboardAsync(TestContext.Current.CancellationToken);

        // Assert: served from cache, not re-downloaded, and not flagged stale (the skip is
        // deliberate policy, not a fetch failure).
        Assert.DoesNotContain(handler.RequestedUrls, url => url.Contains("/p/cotas", StringComparison.Ordinal));
        Assert.False(snapshot.Cotas.IsStale);
        Assert.Equal("Rua Cache", snapshot.Cotas.Value!.Single().Logradouro);
    }

    [Fact]
    public async Task GetDashboardAsync_Should_FetchCotas_When_CachedCopyIsOlderThanTtl()
    {
        // Arrange
        var handler = StubHttpMessageHandler.Healthy();
        var cache = new FakeDashboardCache
        {
            Seeded = new DashboardSnapshot
            {
                Cotas = SectionResult<IReadOnlyList<CotaEnchente>>.Stale(
                    [new CotaEnchente { Logradouro = "Rua Velha" }], DateTimeOffset.Now.AddHours(-25)),
            },
        };
        var service = CreateService(handler, cache);

        // Act
        await service.GetDashboardAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(handler.RequestedUrls, url => url.Contains("/p/cotas", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetDashboardAsync_Should_WriteTheMergedSnapshot_BackToCache()
    {
        // Arrange
        var cache = new FakeDashboardCache();
        var service = CreateService(StubHttpMessageHandler.Healthy(), cache);

        // Act
        await service.GetDashboardAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(cache.LastWritten);
        Assert.True(cache.LastWritten.Weather.HasValue);
        Assert.True(cache.LastWritten.River.HasValue);
    }

    #endregion
}
