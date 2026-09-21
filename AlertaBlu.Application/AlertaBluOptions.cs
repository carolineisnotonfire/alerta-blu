using System.Globalization;

namespace AlertaBlu.Application;

/// <summary>
/// Upstream endpoints, coordinates and timeouts, defaulted to today's known-good values and
/// overridable at startup (see <c>AddAlertaBluInfrastructure</c> in the Infrastructure layer) from
/// an embedded <c>appsettings.json</c>, so a hotfix to a changed URL or a bad timeout does not
/// require a full app-store release.
/// </summary>
public sealed class AlertaBluOptions
{
    /// <summary>Configuration section name this binds from.</summary>
    public const string SectionName = "AlertaBlu";

    public string BaseUrl { get; set; } = "https://defesacivil.blumenau.sc.gov.br";

    /// <summary>Blumenau's coordinates, for the Open-Meteo request.</summary>
    public double Latitude { get; set; } = -26.9194;

    public double Longitude { get; set; } = -49.0661;

    /// <summary>Per-request ceiling; the whole refresh is additionally bounded by <see cref="RefreshTimeoutSeconds"/>.</summary>
    public int RequestTimeoutSeconds { get; set; } = 20;

    /// <summary>Ceiling for a whole refresh, so a hung endpoint cannot pin the spinner forever.</summary>
    public int RefreshTimeoutSeconds { get; set; } = 45;

    /// <summary>Hourly temperature readings from the official AlertaBLU station (JSON).</summary>
    public string TemperaturasUrl => $"{BaseUrl}/static/data/temperaturas.json";

    /// <summary>Detailed page: today's min/max plus the 5-day forecast blocks (HTML).</summary>
    public string DetalhadaUrl => $"{BaseUrl}/p/detalhada";

    /// <summary>River level history; the first row is the latest reading (HTML).</summary>
    public string NivelDoRioUrl => $"{BaseUrl}/d/nivel-do-rio";

    /// <summary>Flood thresholds per street (HTML, ~700 KB).</summary>
    public string CotasUrl => $"{BaseUrl}/p/cotas";

    /// <summary>Upstream dam reservoir status (HTML).</summary>
    public string BarragensUrl => $"{BaseUrl}/d/barragens";

    /// <summary>Official level bands (<c>condicoes</c>) plus the recent level series (JSON).</summary>
    public string NivelOficialUrl => $"{BaseUrl}/static/data/nivel_oficial.json";

    /// <summary>
    /// Sensação térmica and humidity for Blumenau, for today (<c>current</c>) and for each of the
    /// next five days (<c>daily</c>). AlertaBLU publishes neither value anywhere, so these fields
    /// alone come from Open-Meteo (free, no API key).
    /// </summary>
    public string OpenMeteoUrl =>
        "https://api.open-meteo.com/v1/forecast" +
        $"?latitude={Latitude.ToString(CultureInfo.InvariantCulture)}" +
        $"&longitude={Longitude.ToString(CultureInfo.InvariantCulture)}" +
        "&current=relative_humidity_2m,apparent_temperature" +
        "&daily=apparent_temperature_max,relative_humidity_2m_mean" +
        "&forecast_days=5" +
        "&timezone=America%2FSao_Paulo";
}
