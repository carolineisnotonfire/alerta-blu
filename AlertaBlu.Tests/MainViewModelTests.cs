using AlertaBlu.Application;
using AlertaBlu.Domain;
using AlertaBlu.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AlertaBlu.Tests;

/// <summary>
/// Covers <see cref="MainViewModel"/> logic that was previously untestable: it lived in the MAUI
/// head, which multi-targets platform TFMs the plain net10.0 test project cannot reference. Now
/// that it lives in AlertaBlu.Presentation, it can be exercised directly against a fake
/// <see cref="LoadDashboardUseCase"/> pipeline (fake gateway + in-memory cache + fake clock), with
/// no MAUI runtime involved.
/// </summary>
public class MainViewModelTests
{
    private static MainViewModel CreateViewModel(
        FakeAlertaBluGateway? gateway = null, FakeDashboardCache? cache = null, FakeClock? clock = null)
    {
        clock ??= new FakeClock();
        var useCase = new LoadDashboardUseCase(gateway ?? new FakeAlertaBluGateway(), cache ?? new FakeDashboardCache(), clock);
        return new MainViewModel(useCase, clock, new AlertaBluOptions(), NullLogger<MainViewModel>.Instance);
    }

    #region Hero card fallback

    [Fact]
    public async Task HeroTemperatureDisplay_Should_ShowStationReading_When_TodayIsSelected()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        await viewModel.LoadAsync();

        // Assert: today (index 0) has a live station reading (16,04 °C), so that wins.
        Assert.Equal("16°C", viewModel.HeroTemperatureDisplay);
    }

    [Fact]
    public async Task HeroTemperatureDisplay_Should_FallBackToForecastMaximum_When_AFutureDayIsSelected()
    {
        // Arrange: a future day has no station reading, only its forecast maximum.
        var gateway = new FakeAlertaBluGateway
        {
            Detalhada = new DetalhadaResult(
                SectionResult<IReadOnlyList<DailyForecast>>.Ok(
                [
                    new DailyForecast { Date = new DateOnly(2026, 8, 13), Max = new TemperatureRange(21, 23) },
                    new DailyForecast { Date = new DateOnly(2026, 8, 14), Max = new TemperatureRange(18, 20) },
                ]),
                new DayExtremes(14, 22)),
        };
        var viewModel = CreateViewModel(gateway);
        await viewModel.LoadAsync();

        // Act
        viewModel.SelectDay(1);

        // Assert: the top of tomorrow's forecast range (20), not today's live temperature.
        Assert.Equal("20°C", viewModel.HeroTemperatureDisplay);
        Assert.Equal("14/08", viewModel.HeroDateBadge);
    }

    [Fact]
    public async Task HeroFeelsLikeDisplay_Should_ReportUnavailable_When_NeitherSourceHasIt()
    {
        // Arrange: a future day with no forecast sensação térmica and no live reading applies to it.
        var gateway = new FakeAlertaBluGateway
        {
            Detalhada = new DetalhadaResult(
                SectionResult<IReadOnlyList<DailyForecast>>.Ok(
                [
                    new DailyForecast { Date = new DateOnly(2026, 8, 13) },
                    new DailyForecast { Date = new DateOnly(2026, 8, 14) },
                ]),
                null),
        };
        var viewModel = CreateViewModel(gateway);
        await viewModel.LoadAsync();

        // Act
        viewModel.SelectDay(1);

        // Assert
        Assert.Equal("indisponível", viewModel.HeroFeelsLikeDisplay);
    }

    #endregion

    #region Forecast-day selection across refresh

    [Fact]
    public async Task SelectDay_Should_PersistAcrossRefresh_When_TheNewForecastIsStillLongEnough()
    {
        // Arrange
        var gateway = new FakeAlertaBluGateway
        {
            Detalhada = new DetalhadaResult(
                SectionResult<IReadOnlyList<DailyForecast>>.Ok(
                [
                    new DailyForecast { Date = new DateOnly(2026, 8, 13) },
                    new DailyForecast { Date = new DateOnly(2026, 8, 14) },
                    new DailyForecast { Date = new DateOnly(2026, 8, 15) },
                ]),
                new DayExtremes(14, 22)),
        };
        var viewModel = CreateViewModel(gateway);
        await viewModel.LoadAsync();
        viewModel.SelectDay(2);

        // Act: a second refresh with the same three days.
        await viewModel.LoadAsync();

        // Assert: still looking at the third day, not reset to today.
        Assert.Equal(2, viewModel.SelectedForecastIndex);
        Assert.True(viewModel.ForecastDays[2].IsSelected);
        Assert.False(viewModel.ForecastDays[0].IsSelected);
    }

    [Fact]
    public async Task SelectDay_Should_ResetToToday_When_TheNewForecastIsShorterThanTheSelection()
    {
        // Arrange
        var gateway = new FakeAlertaBluGateway
        {
            Detalhada = new DetalhadaResult(
                SectionResult<IReadOnlyList<DailyForecast>>.Ok(
                [
                    new DailyForecast { Date = new DateOnly(2026, 8, 13) },
                    new DailyForecast { Date = new DateOnly(2026, 8, 14) },
                    new DailyForecast { Date = new DateOnly(2026, 8, 15) },
                ]),
                new DayExtremes(14, 22)),
        };
        var viewModel = CreateViewModel(gateway);
        await viewModel.LoadAsync();
        viewModel.SelectDay(2);

        // Act: the next refresh's forecast only covers one day.
        gateway.Detalhada = new DetalhadaResult(
            SectionResult<IReadOnlyList<DailyForecast>>.Ok([new DailyForecast { Date = new DateOnly(2026, 8, 14) }]),
            new DayExtremes(14, 22));
        await viewModel.LoadAsync();

        // Assert
        Assert.Equal(0, viewModel.SelectedForecastIndex);
        Assert.True(viewModel.ForecastDays[0].IsSelected);
    }

    #endregion

    #region Cota filter and count label

    private static FakeAlertaBluGateway GatewayWithThreeCotas() => new()
    {
        Cotas = SectionResult<IReadOnlyList<CotaEnchente>>.Ok(
        [
            new CotaEnchente { Logradouro = "Rua Sao Rafael", Bairro = "Itoupava Norte", CotaMeters = 7.40 },
            new CotaEnchente { Logradouro = "Rua Gustavo Persuhn", Bairro = "Itoupava Seca", CotaMeters = 21.00 },
            new CotaEnchente { Logradouro = "Avenida Beira Rio", Bairro = "Centro", CotaMeters = 3.10 },
        ]),
    };

    [Fact]
    public async Task CotasCountLabel_Should_ShowTotal_When_SearchIsEmpty()
    {
        // Arrange
        var viewModel = CreateViewModel(GatewayWithThreeCotas());

        // Act
        await viewModel.LoadAsync();

        // Assert
        Assert.Equal("3 ruas", viewModel.CotasCountLabel);
    }

    [Fact]
    public async Task CotaSearch_Should_FilterByStreetOrNeighbourhood_CaseInsensitively()
    {
        // Arrange
        var viewModel = CreateViewModel(GatewayWithThreeCotas());
        await viewModel.LoadAsync();

        // Act
        viewModel.CotaSearch = "ITOUPAVA";

        // Assert
        Assert.Equal(2, viewModel.Cotas.Count);
        Assert.Equal("2 de 3", viewModel.CotasCountLabel);
    }

    [Fact]
    public async Task CotaSearch_Should_ReportNoMatch_Rather_Than_NoData_When_FilterExcludesEverything()
    {
        // Arrange
        var viewModel = CreateViewModel(GatewayWithThreeCotas());
        await viewModel.LoadAsync();

        // Act
        viewModel.CotaSearch = "rua que nao existe";

        // Assert: the table did load — it's the filter that found nothing.
        Assert.True(viewModel.HasAnyCotas);
        Assert.True(viewModel.HasNoCotaMatch);
        Assert.False(viewModel.HasCotas);
    }

    [Fact]
    public async Task CotaSearch_Should_ClearToTheFullList_When_TheBoxIsEmptied()
    {
        // Arrange
        var viewModel = CreateViewModel(GatewayWithThreeCotas());
        await viewModel.LoadAsync();
        viewModel.CotaSearch = "Centro";

        // Act
        viewModel.CotaSearch = string.Empty;

        // Assert
        Assert.Equal(3, viewModel.Cotas.Count);
        Assert.Equal("3 ruas", viewModel.CotasCountLabel);
    }

    #endregion

    #region Change notification

    [Fact]
    public async Task LoadAsync_Should_RaisePropertyChanged_ForARepresentativePropertyOfEachSection()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var raised = new HashSet<string>();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is { } name)
            {
                raised.Add(name);
            }
        };

        // Act
        await viewModel.LoadAsync();

        // Assert: one property per section, plus a hero-card property that depends on two of
        // them, plus the page-level fields set outside any single section's Apply method.
        string[] expected =
        [
            nameof(MainViewModel.Weather), nameof(MainViewModel.Forecast), nameof(MainViewModel.River),
            nameof(MainViewModel.RiverThresholds), nameof(MainViewModel.Cotas), nameof(MainViewModel.Dams),
            nameof(MainViewModel.HeroTemperatureDisplay), nameof(MainViewModel.LastUpdatedLabel),
            nameof(MainViewModel.GlobalError), nameof(MainViewModel.IsInitialLoading),
        ];
        Assert.All(expected, name => Assert.Contains(name, raised));
    }

    [Fact]
    public async Task LoadAsync_Should_RaiseGlobalError_When_TheRefreshItselfThrows()
    {
        // Arrange: a gateway whose temperature call throws synchronously inside the use case.
        var gateway = new FakeAlertaBluGateway { TemperatureGate = Task.FromException(new InvalidOperationException("boom")) };
        var viewModel = CreateViewModel(gateway);
        var raised = new HashSet<string>();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is { } name)
            {
                raised.Add(name);
            }
        };

        // Act
        await viewModel.LoadAsync();

        // Assert
        Assert.True(viewModel.HasGlobalError);
        Assert.Contains(nameof(MainViewModel.GlobalError), raised);
        Assert.Contains(nameof(MainViewModel.HasGlobalError), raised);
    }

    #endregion

    #region Re-entrancy guard

    [Fact]
    public async Task LoadAsync_Should_IgnoreAnOverlappingCall_While_ARefreshIsAlreadyInFlight()
    {
        // Arrange: the first call is held open on the temperature fetch.
        var gate = new TaskCompletionSource();
        var gateway = new FakeAlertaBluGateway { TemperatureGate = gate.Task };
        var viewModel = CreateViewModel(gateway);

        var first = viewModel.LoadAsync();

        // Act: an overlapping call (e.g. pull-to-refresh during the initial load).
        var second = viewModel.LoadAsync();

        // Assert: the guard makes the second call a same-thread no-op, so it is already done —
        // it never actually waited on the in-flight fetch.
        Assert.True(second.IsCompleted);
        Assert.False(first.IsCompleted);

        // Cleanup: release the first call so it doesn't leak into other tests.
        gate.SetResult();
        await first;
    }

    [Fact]
    public async Task LoadAsync_Should_ResetIsRefreshing_When_AnOverlappingCallIsIgnored()
    {
        // Arrange: the first call is held open on the temperature fetch.
        var gate = new TaskCompletionSource();
        var gateway = new FakeAlertaBluGateway { TemperatureGate = gate.Task };
        var viewModel = CreateViewModel(gateway);
        var first = viewModel.LoadAsync();

        // RefreshView's two-way binding flips IsRefreshing to true as soon as the user pulls,
        // before the command (and therefore this second LoadAsync call) even runs.
        viewModel.IsRefreshing = true;

        // Act
        var second = viewModel.LoadAsync();

        // Assert: the guard resets the flag it did not set, so the pull-to-refresh spinner does
        // not stay stuck until the original call eventually finishes.
        Assert.True(second.IsCompleted);
        Assert.False(viewModel.IsRefreshing);

        // Cleanup
        gate.SetResult();
        await first;
    }

    [Fact]
    public async Task LoadAsync_Should_AcceptANewCall_Once_ThePreviousRefreshHasFinished()
    {
        // Arrange
        var viewModel = CreateViewModel();
        await viewModel.LoadAsync();

        // Act
        await viewModel.LoadAsync();

        // Assert: no exception, and the view model still reflects a completed load.
        Assert.False(viewModel.IsInitialLoading);
        Assert.True(viewModel.HasWeather);
    }

    #endregion
}
