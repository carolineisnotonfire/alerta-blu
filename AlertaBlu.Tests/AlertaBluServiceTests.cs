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
    private static AlertaBluService CreateService(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler), NullLogger<AlertaBluService>.Instance);

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
        Assert.True(snapshot.Cotas.HasValue);
        Assert.True(snapshot.Barragens.HasValue);

        Assert.Equal(16.04, snapshot.Weather.Value!.TemperatureC!.Value, precision: 2);
        Assert.Equal(93, snapshot.Weather.Value.HumidityPercent);
        Assert.Equal(14d, snapshot.Weather.Value.MinC);
        Assert.Equal(5, snapshot.Forecast.Value!.Count);
        Assert.Equal(3, snapshot.Barragens.Value!.Count);
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
        Assert.Equal(6, handler.RequestedUrls.Count);
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
}
