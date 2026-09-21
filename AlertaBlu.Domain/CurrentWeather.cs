using AlertaBlu.Domain.Common;

namespace AlertaBlu.Domain;

/// <summary>
/// The snapshot shown in the top card. Every field is optional on purpose: the values are
/// stitched together from three independent upstream sources (the AlertaBLU station feed, the
/// scraped "detalhada" page and Open-Meteo), and any one of them can be unavailable without
/// invalidating the others.
/// </summary>
public sealed record CurrentWeather
{
    /// <summary>Latest reading from the official AlertaBLU station.</summary>
    public double? TemperatureC { get; init; }

    /// <summary>Local (UTC-3) timestamp of <see cref="TemperatureC"/>.</summary>
    public DateTimeOffset? ReadingTimeLocal { get; init; }

    /// <summary>Sensação térmica. Sourced from Open-Meteo: AlertaBLU does not publish it.</summary>
    public double? FeelsLikeC { get; init; }

    /// <summary>Umidade relativa. Sourced from Open-Meteo: AlertaBLU does not publish it.</summary>
    public int? HumidityPercent { get; init; }

    /// <summary>Forecast minimum for today, scraped from the "detalhada" page.</summary>
    public double? MinC { get; init; }

    /// <summary>Forecast maximum for today, scraped from the "detalhada" page.</summary>
    public double? MaxC { get; init; }

    /// <summary>True when at least one upstream source produced a value.</summary>
    public bool HasAnyValue =>
        TemperatureC is not null || FeelsLikeC is not null ||
        HumidityPercent is not null || MinC is not null || MaxC is not null;

    public string TemperatureDisplay =>
        TemperatureC is { } value ? $"{PtBr.Format(value, 0)}°C" : "--";

    public string FeelsLikeDisplay =>
        FeelsLikeC is { } value ? $"{PtBr.Format(value, 1)}°C" : "indisponível";

    public string HumidityDisplay =>
        HumidityPercent is { } value ? $"{value}%" : "indisponível";

    public string MaxDisplay => MaxC is { } value ? $"{PtBr.Format(value, 0)}°C" : "--";

    public string MinDisplay => MinC is { } value ? $"{PtBr.Format(value, 0)}°C" : "--";

    /// <summary>Date badge shown in the corner of the card, e.g. <c>13/08</c>.</summary>
    public string DateBadge => (ReadingTimeLocal?.DateTime ?? DateTime.Now).ToString("dd/MM");

    /// <summary>Time of the station reading, e.g. <c>08:00</c>.</summary>
    public string ReadingTimeDisplay =>
        ReadingTimeLocal is { } value ? value.ToString("HH:mm") : "--:--";
}
