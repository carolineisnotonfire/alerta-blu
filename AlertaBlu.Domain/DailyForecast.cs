namespace AlertaBlu.Domain;

/// <summary>
/// One card in the 5-day forecast strip. Display formatting (labels, icon selection, the excerpt
/// fallback) lives on the Presentation-layer <c>ForecastDayViewModel</c> instead.
/// </summary>
/// <remarks>
/// The site groups the 5 days into 4 blocks, the last of which covers two dates. Two
/// <see cref="DailyForecast"/> instances are produced from such a block, sharing the same
/// <see cref="Description"/>.
/// </remarks>
public sealed record DailyForecast
{
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
}
