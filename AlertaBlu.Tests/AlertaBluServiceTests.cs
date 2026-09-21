using AlertaBlu.Application;
using AlertaBlu.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AlertaBlu.Tests;

/// <summary>
/// Covers <see cref="AlertaBluService"/> purely as an <c>IAlertaBluGateway</c>: does it fetch the
/// right URL, parse the response, and turn a failure into the right <c>SectionResult</c>.
/// Composition across sections (weather fusion, forecast merging, caching) is
/// <c>LoadDashboardUseCase</c>'s job and is covered in <see cref="LoadDashboardUseCaseTests"/>.
/// </summary>
public class AlertaBluServiceTests
{
    private static AlertaBluService CreateGateway(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler), new AlertaBluOptions(), NullLogger<AlertaBluService>.Instance);

    [Fact]
    public async Task GetTemperatureAsync_Should_ReturnLatestReading_When_FeedIsHealthy()
    {
        // Act
        var result = await CreateGateway(StubHttpMessageHandler.Healthy())
            .GetTemperatureAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.HasValue);
        Assert.Equal(16.04, result.Value!.ValueC, precision: 2);
    }

    [Fact]
    public async Task GetTemperatureAsync_Should_Fail_With_HttpStatus_When_EndpointIsDown()
    {
        // Act
        var result = await CreateGateway(StubHttpMessageHandler.Healthy().Without("temperaturas.json"))
            .GetTemperatureAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.HasValue);
        Assert.Contains("503", result.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetDetalhadaAsync_Should_SplitForecastAndExtremes_FromOnePageFetch()
    {
        // Arrange
        var handler = StubHttpMessageHandler.Healthy();

        // Act
        var result = await CreateGateway(handler).GetDetalhadaAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Forecast.HasValue);
        Assert.Equal(5, result.Forecast.Value!.Count);
        Assert.Equal(14d, result.Extremes!.MinC);
        Assert.Equal(22d, result.Extremes.MaxC);
        Assert.Equal(1, handler.RequestedUrls.Count(url => url.Contains("/p/detalhada", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task GetDetalhadaAsync_Should_FailForecast_And_NullExtremes_When_PageIsDown()
    {
        // Act
        var result = await CreateGateway(StubHttpMessageHandler.Healthy().Without("/p/detalhada"))
            .GetDetalhadaAsync(TestContext.Current.CancellationToken);

        // Assert: the page fetch itself failed, so neither half has anything to parse.
        Assert.False(result.Forecast.HasValue);
        Assert.Null(result.Extremes);
    }

    [Fact]
    public async Task GetOpenMeteoAsync_Should_SplitApparentAndDaily_FromOneFetch()
    {
        // Act
        var result = await CreateGateway(StubHttpMessageHandler.Healthy())
            .GetOpenMeteoAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Apparent.HasValue);
        Assert.Equal(93, result.Apparent.Value!.HumidityPercent);
        Assert.Equal(5, result.Daily.Count);
    }

    [Fact]
    public async Task GetOpenMeteoAsync_Should_FailApparent_And_EmptyDaily_When_EndpointIsDown()
    {
        // Act
        var result = await CreateGateway(StubHttpMessageHandler.Healthy().Without("api.open-meteo.com"))
            .GetOpenMeteoAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Apparent.HasValue);
        Assert.Empty(result.Daily);
    }

    [Fact]
    public async Task GetRiverLevelAsync_Should_ReturnLatestReading()
    {
        // Act
        var result = await CreateGateway(StubHttpMessageHandler.Healthy())
            .GetRiverLevelAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2.25, result.Value!.LevelMeters, precision: 2);
    }

    [Fact]
    public async Task GetRiverThresholdsAsync_Should_ReturnEveryBand()
    {
        // Act
        var result = await CreateGateway(StubHttpMessageHandler.Healthy())
            .GetRiverThresholdsAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, result.Value!.Count);
    }

    [Fact]
    public async Task GetCotasAsync_Should_ReturnEveryRow()
    {
        // Act
        var result = await CreateGateway(StubHttpMessageHandler.Healthy())
            .GetCotasAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, result.Value!.Count);
    }

    [Fact]
    public async Task GetBarragensAsync_Should_ReturnEveryDam()
    {
        // Act
        var result = await CreateGateway(StubHttpMessageHandler.Healthy())
            .GetBarragensAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, result.Value!.Count);
    }

    [Fact]
    public async Task GetBarragensAsync_Should_ReportParseError_When_PayloadShapeChanges()
    {
        // Arrange: the endpoint answers 200 with markup the parser cannot read.
        var handler = StubHttpMessageHandler.Healthy().Respond("/d/barragens", "<html><body>redesenhado</body></html>");

        // Act
        var result = await CreateGateway(handler).GetBarragensAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.HasValue);
        Assert.True(result.HasError);
    }

    [Fact]
    public async Task GetTemperatureAsync_Should_PropagateCancellation()
    {
        // Arrange
        var gateway = CreateGateway(new StubHttpMessageHandler { BlockUntilCancelled = true });
        using var cancellation = new CancellationTokenSource();

        // Act
        var pending = gateway.GetTemperatureAsync(cancellation.Token);
        await cancellation.CancelAsync();

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }
}
