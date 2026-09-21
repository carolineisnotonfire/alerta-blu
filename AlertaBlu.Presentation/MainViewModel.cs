using System.Windows.Input;
using AlertaBlu.Application;
using AlertaBlu.Domain;
using Microsoft.Extensions.Logging;

namespace AlertaBlu.ViewModels;

/// <summary>
/// State for the home screen. Hand-rolled <see cref="System.ComponentModel.INotifyPropertyChanged"/>
/// (via <see cref="ObservableBase"/>) rather than an MVVM framework: this is a single-page app and
/// the extra dependency would not earn its keep.
/// </summary>
public sealed class MainViewModel : ObservableBase
{
    /// <summary>Ceiling for a whole refresh, so a hung endpoint cannot pin the spinner forever.</summary>
    private static readonly TimeSpan RefreshTimeout = TimeSpan.FromSeconds(45);

    private readonly LoadDashboardUseCase _loadDashboard;
    private readonly IClock _clock;
    private readonly ILogger<MainViewModel> _logger;

    private CancellationTokenSource? _cancellation;
    private bool _isLoading;
    private bool _isRefreshing;
    private bool _hasLoadedOnce;
    private bool _isRiverExpanded;
    private int _selectedForecastIndex;
    private string _cotaSearch = string.Empty;
    private IReadOnlyList<CotaEnchente> _allCotas = [];

    /// <summary>
    /// Lowercase "street neighbourhood" haystack per row in <see cref="_allCotas"/>, same index,
    /// computed once per load rather than once per keystroke (the table carries ~1900 rows). Kept
    /// here rather than on <see cref="CotaEnchente"/> itself: a lazily-computed field on a record
    /// breaks its synthesized value equality, since C# generates <c>Equals</c>/<c>GetHashCode</c>
    /// over every field, mutable ones included.
    /// </summary>
    private string[] _allCotaSearchIndex = [];

    public MainViewModel(LoadDashboardUseCase loadDashboard, IClock clock, ILogger<MainViewModel> logger)
    {
        _loadDashboard = loadDashboard;
        _clock = clock;
        _logger = logger;
        RefreshCommand = new RelayCommand(async () => await LoadAsync().ConfigureAwait(false));
        ToggleRiverCommand = new RelayCommand(() => IsRiverExpanded = !IsRiverExpanded);
    }

    public ICommand RefreshCommand { get; }

    public ICommand ToggleRiverCommand { get; }

    #region Sections

    public CurrentWeather? Weather { get; private set; }

    public string? WeatherError { get; private set; }

    public bool HasWeather => Weather is not null;

    public bool HasWeatherError => WeatherError is not null;

    /// <summary>Set when <see cref="Weather"/> is cached data shown because the live fetch failed.</summary>
    public string? WeatherStaleLabel { get; private set; }

    public bool HasWeatherStale => WeatherStaleLabel is not null;

    public IReadOnlyList<DailyForecast> Forecast { get; private set; } = [];

    /// <summary>The forecast wrapped for the chip strip, one selectable row per day.</summary>
    public IReadOnlyList<ForecastDayViewModel> ForecastDays { get; private set; } = [];

    public string? ForecastError { get; private set; }

    public bool HasForecast => Forecast.Count > 0;

    public bool HasForecastError => ForecastError is not null;

    public string? ForecastStaleLabel { get; private set; }

    public bool HasForecastStale => ForecastStaleLabel is not null;

    public RiverLevel? River { get; private set; }

    public string? RiverError { get; private set; }

    public bool HasRiver => River is not null;

    public bool HasRiverError => RiverError is not null;

    public string? RiverStaleLabel { get; private set; }

    public bool HasRiverStale => RiverStaleLabel is not null;

    /// <summary>
    /// Surfaced on the page view model so the trend arrow can switch on it even before the first
    /// reading arrives, when <see cref="River"/> is still null.
    /// </summary>
    public RiverTrend RiverTrend => River?.Trend ?? RiverTrend.Unknown;

    /// <summary>Official level bands, with the one holding the current reading flagged.</summary>
    public IReadOnlyList<RiverThreshold> RiverThresholds { get; private set; } = [];

    public string? RiverThresholdsError { get; private set; }

    public bool HasRiverThresholds => RiverThresholds.Count > 0;

    public bool HasRiverThresholdsError => RiverThresholdsError is not null;

    public string? RiverThresholdsStaleLabel { get; private set; }

    public bool HasRiverThresholdsStale => RiverThresholdsStaleLabel is not null;

    /// <summary>Cotas matching <see cref="CotaSearch"/>; the full table when the box is empty.</summary>
    public IReadOnlyList<CotaEnchente> Cotas { get; private set; } = [];

    public string? CotasError { get; private set; }

    public bool HasCotas => Cotas.Count > 0;

    public bool HasCotasError => CotasError is not null;

    public string? CotasStaleLabel { get; private set; }

    public bool HasCotasStale => CotasStaleLabel is not null;

    /// <summary>
    /// Whether the table loaded at all, independently of the current filter. The search box binds
    /// to this rather than to <see cref="HasCotas"/> so that filtering down to zero results cannot
    /// hide the very control needed to clear the filter.
    /// </summary>
    public bool HasAnyCotas => _allCotas.Count > 0;

    /// <summary>True once loaded but filtered down to nothing, to tell "no data" from "no match".</summary>
    public bool HasNoCotaMatch => _allCotas.Count > 0 && Cotas.Count == 0;

    public string CotasCountLabel => _allCotas.Count == 0
        ? string.Empty
        : Cotas.Count == _allCotas.Count
            ? $"{_allCotas.Count} ruas"
            : $"{Cotas.Count} de {_allCotas.Count}";

    /// <summary>The dams, wrapped so each card can expand independently.</summary>
    public IReadOnlyList<DamCardViewModel> Dams { get; private set; } = [];

    public string? BarragensError { get; private set; }

    public bool HasBarragens => Dams.Count > 0;

    public bool HasBarragensError => BarragensError is not null;

    public string? BarragensStaleLabel { get; private set; }

    public bool HasBarragensStale => BarragensStaleLabel is not null;

    #endregion

    #region Hero card

    /// <summary>
    /// Which day the hero card is showing. Index 0 is today, the only day for which live
    /// measurements exist; a future day falls back to its forecast figures.
    /// </summary>
    public int SelectedForecastIndex => _selectedForecastIndex;

    public bool HasHero => Weather is not null || SelectedDay is not null;

    public WeatherIconKey HeroIconKey => SelectedDay?.IconKey ?? WeatherIconKey.Cloud;

    public string HeroDateBadge =>
        SelectedDay?.DayLabel ?? Weather?.DateBadge ?? _clock.Now.ToString("dd/MM");

    /// <summary>
    /// The station reading for today; for a future day there is no measurement, so that day's
    /// forecast maximum takes the headline slot instead.
    /// </summary>
    public string HeroTemperatureDisplay =>
        IsTodaySelected && Weather?.TemperatureC is not null
            ? Weather.TemperatureDisplay
            : SelectedDay?.HeadlineTemperature ?? Weather?.TemperatureDisplay ?? "--";

    public string HeroFeelsLikeDisplay =>
        IsTodaySelected && Weather?.FeelsLikeC is not null
            ? Weather.FeelsLikeDisplay
            : SelectedDay?.FeelsLikeDisplay ?? Weather?.FeelsLikeDisplay ?? "indisponível";

    public string HeroMaxDisplay =>
        IsTodaySelected && Weather?.MaxC is not null
            ? Weather.MaxDisplay
            : SelectedDay?.MaxDisplay ?? Weather?.MaxDisplay ?? "--";

    public string HeroMinDisplay =>
        IsTodaySelected && Weather?.MinC is not null
            ? Weather.MinDisplay
            : SelectedDay?.MinDisplay ?? Weather?.MinDisplay ?? "--";

    public string HeroHumidityDisplay =>
        IsTodaySelected && Weather?.HumidityPercent is not null
            ? Weather.HumidityDisplay
            : SelectedDay?.HumidityDisplay ?? Weather?.HumidityDisplay ?? "indisponível";

    private bool IsTodaySelected => _selectedForecastIndex == 0;

    private DailyForecast? SelectedDay =>
        (uint)_selectedForecastIndex < (uint)Forecast.Count ? Forecast[_selectedForecastIndex] : null;

    /// <summary>
    /// Moves the hero card to another day. Out-of-range indices are ignored rather than clamped:
    /// the only caller is a chip that exists because the day exists.
    /// </summary>
    public void SelectDay(int index)
    {
        if (index < 0 || index >= ForecastDays.Count || index == _selectedForecastIndex)
        {
            return;
        }

        _selectedForecastIndex = index;
        SyncSelection();

        OnPropertyChanged(nameof(SelectedForecastIndex));
        RaiseHeroPropertiesChanged();
    }

    #endregion

    #region UI state

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    /// <summary>Whether the river card is showing its official level bands.</summary>
    public bool IsRiverExpanded
    {
        get => _isRiverExpanded;
        set => SetProperty(ref _isRiverExpanded, value);
    }

    /// <summary>Drives the spinner shown only before the first successful render.</summary>
    public bool IsInitialLoading => _isLoading && !_hasLoadedOnce;

    /// <summary>Set only when the refresh itself failed, as opposed to an individual section.</summary>
    public string? GlobalError { get; private set; }

    public bool HasGlobalError => GlobalError is not null;

    public string LastUpdatedLabel { get; private set; } = string.Empty;

    /// <summary>Free-text filter over street and neighbourhood names.</summary>
    public string CotaSearch
    {
        get => _cotaSearch;
        set
        {
            if (SetProperty(ref _cotaSearch, value))
            {
                ApplyCotaFilter();
            }
        }
    }

    #endregion

    /// <summary>
    /// Refreshes every section. Safe to call repeatedly: overlapping invocations (pull-to-refresh
    /// during the initial load) are ignored rather than queued.
    /// </summary>
    public async Task LoadAsync()
    {
        if (_isLoading)
        {
            return;
        }

        _isLoading = true;
        IsRefreshing = true;
        OnPropertyChanged(nameof(IsInitialLoading));

        try
        {
            _cancellation?.Dispose();
            _cancellation = new CancellationTokenSource(RefreshTimeout);

            var snapshot = await _loadDashboard.ExecuteAsync(_cancellation.Token).ConfigureAwait(true);

            Apply(snapshot);
            _hasLoadedOnce = true;
        }
        catch (OperationCanceledException)
        {
            // Navigated away or timed out; leave whatever is already on screen in place.
            _logger.LogInformation("Atualização cancelada.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha inesperada ao atualizar o painel.");
            GlobalError = "Não foi possível atualizar. Verifique sua conexão e tente novamente.";
        }
        finally
        {
            _isLoading = false;
            IsRefreshing = false;
            RaiseAllPropertiesChanged();
        }
    }

    /// <summary>Cancels an in-flight refresh, e.g. when the page disappears.</summary>
    public void Cancel()
    {
        try
        {
            _cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already torn down; nothing to cancel.
        }
    }

    private void Apply(DashboardSnapshot snapshot)
    {
        GlobalError = null;

        Weather = snapshot.Weather.Value;
        WeatherError = snapshot.Weather.Error;
        WeatherStaleLabel = StaleLabel(snapshot.Weather);

        Forecast = snapshot.Forecast.Value ?? [];
        ForecastError = snapshot.Forecast.Error;
        ForecastStaleLabel = StaleLabel(snapshot.Forecast);
        RebuildForecastDays();

        River = snapshot.River.Value;
        RiverError = snapshot.River.Error;
        RiverStaleLabel = StaleLabel(snapshot.River);

        RiverThresholds = snapshot.RiverThresholds.Value ?? [];
        RiverThresholdsError = snapshot.RiverThresholds.Error;
        RiverThresholdsStaleLabel = StaleLabel(snapshot.RiverThresholds);

        _allCotas = snapshot.Cotas.Value ?? [];
        _allCotaSearchIndex = _allCotas
            .Select(static cota => $"{cota.Logradouro} {cota.Bairro}".ToLowerInvariant())
            .ToArray();
        CotasError = snapshot.Cotas.Error;
        CotasStaleLabel = StaleLabel(snapshot.Cotas);
        ApplyCotaFilter(notify: false);

        Dams = (snapshot.Barragens.Value ?? []).Select(static dam => new DamCardViewModel(dam)).ToArray();
        BarragensError = snapshot.Barragens.Error;
        BarragensStaleLabel = StaleLabel(snapshot.Barragens);

        LastUpdatedLabel = $"atualizado às {snapshot.LoadedAt:HH:mm}";
    }

    /// <summary>
    /// "dados de HH:mm" when <paramref name="section"/> is cached data shown because the live
    /// fetch failed, so the section still renders instead of going blank; null otherwise.
    /// </summary>
    private static string? StaleLabel<T>(SectionResult<T> section) where T : class =>
        section.StaleAsOf is { } asOf ? $"dados de {asOf.LocalDateTime:HH:mm}" : null;

    /// <summary>
    /// Rebuilds the chip strip after a refresh, keeping the day the user was looking at whenever
    /// the new forecast is long enough to still have it.
    /// </summary>
    private void RebuildForecastDays()
    {
        ForecastDays = Forecast
            .Select((day, index) => new ForecastDayViewModel(day, index, selected => SelectDay(selected.Index)))
            .ToArray();

        if (_selectedForecastIndex >= ForecastDays.Count)
        {
            _selectedForecastIndex = 0;
        }

        SyncSelection();
    }

    private void SyncSelection()
    {
        foreach (var day in ForecastDays)
        {
            day.IsSelected = day.Index == _selectedForecastIndex;
        }
    }

    /// <summary>
    /// Rebuilds the visible cota list. The table carries roughly 1900 rows, so the filter runs
    /// against each row's pre-lowercased search index instead of re-casing on every keystroke.
    /// </summary>
    private void ApplyCotaFilter(bool notify = true)
    {
        var needle = _cotaSearch.Trim().ToLowerInvariant();

        Cotas = needle.Length == 0
            ? _allCotas
            : _allCotas
                .Where((_, index) => _allCotaSearchIndex[index].Contains(needle, StringComparison.Ordinal))
                .ToArray();

        if (notify)
        {
            OnPropertyChanged(nameof(Cotas));
            OnPropertyChanged(nameof(HasCotas));
            OnPropertyChanged(nameof(HasNoCotaMatch));
            OnPropertyChanged(nameof(CotasCountLabel));
        }
    }

    /// <summary>Everything the hero card reads, re-raised whenever the selected day changes.</summary>
    private static readonly string[] HeroProperties =
    [
        nameof(HasHero), nameof(HeroIconKey), nameof(HeroDateBadge), nameof(HeroTemperatureDisplay),
        nameof(HeroFeelsLikeDisplay), nameof(HeroMaxDisplay), nameof(HeroMinDisplay),
        nameof(HeroHumidityDisplay),
    ];

    /// <summary>Every property a completed refresh can change.</summary>
    private static readonly string[] RefreshableProperties =
    [
        nameof(Weather), nameof(WeatherError), nameof(HasWeather), nameof(HasWeatherError),
        nameof(WeatherStaleLabel), nameof(HasWeatherStale),
        nameof(Forecast), nameof(ForecastDays), nameof(ForecastError), nameof(HasForecast),
        nameof(HasForecastError), nameof(ForecastStaleLabel), nameof(HasForecastStale),
        nameof(SelectedForecastIndex),
        nameof(River), nameof(RiverError), nameof(HasRiver), nameof(HasRiverError), nameof(RiverTrend),
        nameof(RiverStaleLabel), nameof(HasRiverStale),
        nameof(RiverThresholds), nameof(RiverThresholdsError), nameof(HasRiverThresholds),
        nameof(HasRiverThresholdsError), nameof(RiverThresholdsStaleLabel), nameof(HasRiverThresholdsStale),
        nameof(Cotas), nameof(CotasError), nameof(HasCotas), nameof(HasCotasError),
        nameof(HasAnyCotas), nameof(HasNoCotaMatch), nameof(CotasCountLabel),
        nameof(CotasStaleLabel), nameof(HasCotasStale),
        nameof(Dams), nameof(BarragensError), nameof(HasBarragens), nameof(HasBarragensError),
        nameof(BarragensStaleLabel), nameof(HasBarragensStale),
        nameof(GlobalError), nameof(HasGlobalError), nameof(LastUpdatedLabel), nameof(IsInitialLoading),
    ];

    private void RaiseHeroPropertiesChanged()
    {
        foreach (var property in HeroProperties)
        {
            OnPropertyChanged(property);
        }
    }

    /// <summary>
    /// Signals that every binding should re-read after a refresh that touches the whole screen.
    /// The properties are named explicitly rather than relying on the empty-name "all changed"
    /// convention, so the refresh does not depend on how the binding engine interprets it.
    /// </summary>
    private void RaiseAllPropertiesChanged()
    {
        foreach (var property in RefreshableProperties)
        {
            OnPropertyChanged(property);
        }

        RaiseHeroPropertiesChanged();
    }
}
