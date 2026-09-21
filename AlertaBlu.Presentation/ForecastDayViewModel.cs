using System.Windows.Input;
using AlertaBlu.Domain;
using AlertaBlu.Domain.Common;

namespace AlertaBlu.ViewModels;

/// <summary>
/// One chip in the 5-day strip: the immutable scraped day plus the selection state the chip
/// needs to paint itself, and the display formatting that used to live on the Domain record.
/// </summary>
/// <remarks>
/// <see cref="DailyForecast"/> is a record shared with the parsing layer and deliberately has no
/// UI state; selection is per-row and mutable, so it lives here instead.
/// </remarks>
public sealed class ForecastDayViewModel : ObservableBase
{
    private const int SummaryMaxLength = 90;

    private bool _isSelected;

    public ForecastDayViewModel(DailyForecast data, int index, Action<ForecastDayViewModel> onSelected)
    {
        Data = data;
        Index = index;
        SelectCommand = new RelayCommand(() => onSelected(this));
    }

    public DailyForecast Data { get; }

    /// <summary>Position in the strip; index 0 is today, the only day with live measurements.</summary>
    public int Index { get; }

    public ICommand SelectCommand { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string DayLabel => Data.Date.ToString("dd/MM");

    public string MaxDisplay => Data.Max?.Display ?? "--";

    public string MinDisplay => Data.Min?.Display ?? "--";

    public bool HasTemperatures => Data.Min is not null || Data.Max is not null;

    /// <summary>Drives the excerpt fallback shown when neither temperature regex matched.</summary>
    public bool HasNoTemperatures => !HasTemperatures;

    public string FeelsLikeDisplay =>
        Data.FeelsLikeC is { } value ? $"{PtBr.Format(value, 1)}°C" : "indisponível";

    public string HumidityDisplay =>
        Data.HumidityPercent is { } value ? $"{value}%" : "indisponível";

    /// <summary>Decorative icon for this day, inferred from the forecast description.</summary>
    public WeatherIconKey IconKey => WeatherIcons.KeyFor(Data.Description);

    /// <summary>
    /// The single temperature shown on the day chip. A forecast day has no measured temperature,
    /// so the top of its forecast maximum stands in.
    /// </summary>
    public string TemperatureLabel => TopTemperature is { } value ? $"{PtBr.Format(value, 0)}°" : "--";

    /// <summary>Same figure as <see cref="TemperatureLabel"/>, sized for the hero card.</summary>
    public string HeadlineTemperature => TopTemperature is { } value ? $"{PtBr.Format(value, 0)}°C" : "--";

    private double? TopTemperature => Data.Max?.High ?? Data.Min?.High;

    /// <summary>
    /// Short excerpt of the forecast description for the compact day card. Doubles as the
    /// fallback content when the temperature regexes find nothing.
    /// </summary>
    public string Summary
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Data.Description))
            {
                return string.Empty;
            }

            var text = Data.Description.Trim();
            if (text.Length <= SummaryMaxLength)
            {
                return text;
            }

            // Prefer cutting on a word boundary so the excerpt does not end mid-word.
            var cut = text.LastIndexOf(' ', SummaryMaxLength);
            return string.Concat(text.AsSpan(0, cut > 0 ? cut : SummaryMaxLength).TrimEnd(), "…");
        }
    }
}
