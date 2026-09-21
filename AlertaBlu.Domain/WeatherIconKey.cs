namespace AlertaBlu.Domain;

/// <summary>
/// Which of the four line icons illustrates a day in the forecast strip.
/// </summary>
/// <remarks>
/// The geometry for each key lives in <c>MainPage.xaml</c> as a <c>PathGeometry</c> resource;
/// only the choice is modelled here so it can be unit-tested without a UI.
/// </remarks>
public enum WeatherIconKey
{
    Cloud,
    Rain,
    Sun,
    CloudSun,
}

/// <summary>
/// Picks an icon from the free-text forecast the site publishes.
/// </summary>
/// <remarks>
/// Purely decorative and therefore deliberately best-effort: AlertaBLU publishes no condition
/// code, only prose such as "Nesta quinta-feira, ocorrem algumas aberturas de sol". The keywords
/// are chosen to be accent-free substrings ("chuv", "nubl", "sol"), so the match works whether or
/// not the upstream entities decoded cleanly.
/// </remarks>
public static class WeatherIcons
{
    private static readonly string[] RainWords =
        ["chuv", "pancad", "temporal", "tempestade", "trovoad", "precipita", "garoa", "chuvisco"];

    private static readonly string[] SunWords =
        ["sol", "ensolarad", "limpo", "claro"];

    private static readonly string[] CloudWords =
        ["nubl", "nuve", "encobert", "nebulos", "instavel", "instável"];

    /// <summary>
    /// Rain wins over everything (it is the operationally relevant signal on a flood-alert
    /// screen); sun and cloud together give the mixed icon; anything unrecognised falls back to
    /// the neutral cloud.
    /// </summary>
    public static WeatherIconKey KeyFor(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return WeatherIconKey.Cloud;
        }

        var text = description.ToLowerInvariant();

        if (ContainsAny(text, RainWords))
        {
            return WeatherIconKey.Rain;
        }

        var sunny = ContainsAny(text, SunWords);
        var cloudy = ContainsAny(text, CloudWords);

        return (sunny, cloudy) switch
        {
            (true, true) => WeatherIconKey.CloudSun,
            (true, false) => WeatherIconKey.Sun,
            _ => WeatherIconKey.Cloud,
        };
    }

    private static bool ContainsAny(string text, string[] needles)
    {
        foreach (var needle in needles)
        {
            if (text.Contains(needle, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
