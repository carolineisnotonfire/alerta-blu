using System.Text.Json.Serialization;

namespace AlertaBlu.Infrastructure;

/// <summary>One hourly reading from <c>static/data/temperaturas.json</c>.</summary>
internal sealed class TemperaturaDto
{
    [JsonPropertyName("valor")]
    public double Valor { get; set; }

    /// <summary>Published in UTC (trailing "Z"); Blumenau local time is this minus 3 hours.</summary>
    [JsonPropertyName("horaLeitura")]
    public DateTimeOffset HoraLeitura { get; set; }
}

/// <summary>Open-Meteo response, narrowed to the fields AlertaBLU does not publish.</summary>
internal sealed class OpenMeteoResponse
{
    [JsonPropertyName("current")]
    public OpenMeteoCurrent? Current { get; set; }

    [JsonPropertyName("daily")]
    public OpenMeteoDaily? Daily { get; set; }
}

internal sealed class OpenMeteoCurrent
{
    [JsonPropertyName("apparent_temperature")]
    public double? ApparentTemperature { get; set; }

    [JsonPropertyName("relative_humidity_2m")]
    public int? RelativeHumidity { get; set; }
}

/// <summary>
/// The daily block: parallel arrays keyed by position against <see cref="Time"/>, which holds
/// <c>yyyy-MM-dd</c> strings.
/// </summary>
internal sealed class OpenMeteoDaily
{
    [JsonPropertyName("time")]
    public string[]? Time { get; set; }

    [JsonPropertyName("apparent_temperature_max")]
    public double?[]? ApparentTemperatureMax { get; set; }

    [JsonPropertyName("relative_humidity_2m_mean")]
    public double?[]? RelativeHumidityMean { get; set; }
}

/// <summary>Official river-level conditions from <c>nivel_oficial.json</c>.</summary>
internal sealed class NivelOficialResponse
{
    [JsonPropertyName("condicoes")]
    public CondicaoDto[]? Condicoes { get; set; }
}

internal sealed class CondicaoDto
{
    /// <summary>Level in metres at which this condition starts.</summary>
    [JsonPropertyName("nivel")]
    public double Nivel { get; set; }

    [JsonPropertyName("condicao")]
    public string? Condicao { get; set; }
}

/// <summary>
/// Source-generated serialisation metadata. Required rather than optional here: MAUI release
/// builds trim the app, and reflection-based <c>System.Text.Json</c> would break at runtime.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(TemperaturaDto[]))]
[JsonSerializable(typeof(OpenMeteoResponse))]
[JsonSerializable(typeof(NivelOficialResponse))]
internal sealed partial class AlertaBluJsonContext : JsonSerializerContext;
