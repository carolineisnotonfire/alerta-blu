using AlertaBlu.Domain.Common;

namespace AlertaBlu.Domain;

/// <summary>One card in the 5-day forecast strip.</summary>
/// <remarks>
/// The site groups the 5 days into 4 blocks, the last of which covers two dates. Two
/// <see cref="DailyForecast"/> instances are produced from such a block, sharing the same
/// <see cref="Description"/>.
/// </remarks>
public sealed record DailyForecast
{
    private const int SummaryMaxLength = 90;

    public required DateOnly Date { get; init; }

    /// <summary>Free-text forecast published by the site, entity-decoded and whitespace-normalised.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Parsed from "Mínima entre X e YºC" when present.</summary>
    public TemperatureRange? Min { get; init; }

    /// <summary>Parsed from "Máxima entre X e YºC" when present.</summary>
    public TemperatureRange? Max { get; init; }

    /// <summary>
    /// Forecast sensação térmica for this day (Open-Meteo <c>apparent_temperature_max</c>).
    /// AlertaBLU publishes no such value, and it is matched by date, so it stays null whenever the
    /// two sources disagree about which days they cover.
    /// </summary>
    public double? FeelsLikeC { get; init; }

    /// <summary>Forecast mean humidity for this day (Open-Meteo); null when unmatched.</summary>
    public int? HumidityPercent { get; init; }

    public string DayLabel => Date.ToString("dd/MM");

    public string WeekdayLabel => PtBr.Weekday(Date.DayOfWeek);

    public string MaxDisplay => Max?.Display ?? "--";

    public string MinDisplay => Min?.Display ?? "--";

    public bool HasTemperatures => Min is not null || Max is not null;

    /// <summary>Drives the excerpt fallback shown when neither temperature regex matched.</summary>
    public bool HasNoTemperatures => !HasTemperatures;

    public string FeelsLikeDisplay =>
        FeelsLikeC is { } value ? $"{PtBr.Format(value, 1)}°C" : "indisponível";

    public string HumidityDisplay =>
        HumidityPercent is { } value ? $"{value}%" : "indisponível";

    /// <summary>Decorative icon for this day, inferred from <see cref="Description"/>.</summary>
    public WeatherIconKey IconKey => WeatherIcons.KeyFor(Description);

    /// <summary>
    /// The single temperature shown on the day chip. A forecast day has no measured temperature,
    /// so the top of its forecast maximum stands in.
    /// </summary>
    public string ChipTemperature => TopTemperature is { } value ? $"{PtBr.Format(value, 0)}°" : "--";

    /// <summary>Same figure as <see cref="ChipTemperature"/>, sized for the hero card.</summary>
    public string HeadlineTemperature => TopTemperature is { } value ? $"{PtBr.Format(value, 0)}°C" : "--";

    private double? TopTemperature => Max?.High ?? Min?.High;

    /// <summary>
    /// Short excerpt of <see cref="Description"/> for the compact day card. Doubles as the
    /// fallback content when the temperature regexes find nothing.
    /// </summary>
    public string Summary
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Description))
            {
                return string.Empty;
            }

            var text = Description.Trim();
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
