using AlertaBlu.Domain;

namespace AlertaBlu.ViewModels;

/// <summary>
/// One chip in the 5-day strip: the immutable scraped day plus the selection state the chip
/// needs to paint itself.
/// </summary>
/// <remarks>
/// <see cref="DailyForecast"/> is a record shared with the parsing layer and deliberately has no
/// UI state; selection is per-row and mutable, so it lives here instead.
/// </remarks>
public sealed class ForecastDayViewModel : ObservableBase
{
    private bool _isSelected;

    public ForecastDayViewModel(DailyForecast data, int index, Action<ForecastDayViewModel> onSelected)
    {
        Data = data;
        Index = index;
        SelectCommand = new Command(() => onSelected(this));
    }

    public DailyForecast Data { get; }

    /// <summary>Position in the strip; index 0 is today, the only day with live measurements.</summary>
    public int Index { get; }

    public Command SelectCommand { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string DayLabel => Data.DayLabel;

    public string TemperatureLabel => Data.ChipTemperature;

    public WeatherIconKey IconKey => Data.IconKey;
}
