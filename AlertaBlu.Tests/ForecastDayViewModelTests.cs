using AlertaBlu.Domain;
using AlertaBlu.ViewModels;
using Xunit;

namespace AlertaBlu.Tests;

public class ForecastDayViewModelTests
{
    private static ForecastDayViewModel CreateViewModel(DailyForecast data, int index = 0) =>
        new(data, index, static _ => { });

    [Fact]
    public void HasNoTemperatures_Should_BeTrue_When_NeitherRangeWasParsed()
    {
        // Arrange
        var viewModel = CreateViewModel(
            new DailyForecast { Date = new DateOnly(2026, 8, 15), Description = "tempo instável" });

        // Assert: a day the regexes cannot read still renders as date + excerpt.
        Assert.True(viewModel.HasNoTemperatures);
        Assert.Equal("tempo instável", viewModel.Summary);
    }

    [Fact]
    public void Summary_Should_TruncateOnAWordBoundary_When_TheDescriptionExceeds90Characters()
    {
        // Arrange
        var longDescription = string.Join(' ', Enumerable.Repeat("palavra", 20));
        var viewModel = CreateViewModel(
            new DailyForecast { Date = new DateOnly(2026, 8, 13), Description = longDescription });

        // Assert
        Assert.True(viewModel.Summary.Length <= 91); // 90 chars + the ellipsis
        Assert.EndsWith("…", viewModel.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("palavr…", viewModel.Summary, StringComparison.Ordinal); // no mid-word cut
    }

    [Fact]
    public void HeadlineTemperature_Should_UseTheTopOfTheMaximumRange()
    {
        // Arrange
        var viewModel = CreateViewModel(
            new DailyForecast { Date = new DateOnly(2026, 8, 13), Max = new TemperatureRange(18, 20) });

        // Assert
        Assert.Equal("20°C", viewModel.HeadlineTemperature);
    }

    [Fact]
    public void HeadlineTemperature_Should_FallBackToMinimumRange_When_NoMaximumWasParsed()
    {
        // Arrange
        var viewModel = CreateViewModel(
            new DailyForecast { Date = new DateOnly(2026, 8, 13), Min = new TemperatureRange(14, 16) });

        // Assert
        Assert.Equal("16°C", viewModel.HeadlineTemperature);
    }

    [Fact]
    public void FeelsLikeDisplay_And_HumidityDisplay_Should_ReportUnavailable_When_Unmatched()
    {
        // Arrange
        var viewModel = CreateViewModel(new DailyForecast { Date = new DateOnly(2026, 8, 13) });

        // Assert
        Assert.Equal("indisponível", viewModel.FeelsLikeDisplay);
        Assert.Equal("indisponível", viewModel.HumidityDisplay);
    }

    [Theory]
    [InlineData("Nesta quinta, chuva forte à tarde", WeatherIconKey.Rain)]
    [InlineData("Sol o dia todo", WeatherIconKey.Sun)]
    [InlineData("Sol entre nuvens", WeatherIconKey.CloudSun)]
    [InlineData("Encoberto o dia todo", WeatherIconKey.Cloud)]
    public void IconKey_Should_BeInferred_FromTheDescription(string description, WeatherIconKey expected)
    {
        // Arrange
        var viewModel = CreateViewModel(
            new DailyForecast { Date = new DateOnly(2026, 8, 13), Description = description });

        // Assert
        Assert.Equal(expected, viewModel.IconKey);
    }
}
