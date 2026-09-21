namespace AlertaBlu.Infrastructure;

/// <summary>Upstream URLs used by the dashboard.</summary>
public static class AlertaBluEndpoints
{
    public const string Base = "https://defesacivil.blumenau.sc.gov.br";

    /// <summary>Hourly temperature readings from the official AlertaBLU station (JSON).</summary>
    public const string Temperaturas = $"{Base}/static/data/temperaturas.json";

    /// <summary>Detailed page: today's min/max plus the 5-day forecast blocks (HTML).</summary>
    public const string Detalhada = $"{Base}/p/detalhada";

    /// <summary>River level history; the first row is the latest reading (HTML).</summary>
    public const string NivelDoRio = $"{Base}/d/nivel-do-rio";

    /// <summary>Flood thresholds per street (HTML, ~700 KB).</summary>
    public const string Cotas = $"{Base}/p/cotas";

    /// <summary>Upstream dam reservoir status (HTML).</summary>
    public const string Barragens = $"{Base}/d/barragens";

    /// <summary>
    /// Official level bands (<c>condicoes</c>) plus the recent level series (JSON). Only the
    /// bands are used: the current level keeps coming from the <see cref="NivelDoRio"/> scrape,
    /// which also carries the trend and the delta.
    /// </summary>
    public const string NivelOficial = $"{Base}/static/data/nivel_oficial.json";

    /// <summary>
    /// Sensação térmica and humidity for Blumenau, for today (<c>current</c>) and for each of the
    /// next five days (<c>daily</c>). AlertaBLU publishes neither value anywhere, so these fields
    /// alone come from Open-Meteo (free, no API key). One request feeds both the hero card and
    /// the per-day figures behind the forecast chips.
    /// </summary>
    public const string OpenMeteo =
        "https://api.open-meteo.com/v1/forecast" +
        "?latitude=-26.9194&longitude=-49.0661" +
        "&current=relative_humidity_2m,apparent_temperature" +
        "&daily=apparent_temperature_max,relative_humidity_2m_mean" +
        "&forecast_days=5" +
        "&timezone=America%2FSao_Paulo";
}
