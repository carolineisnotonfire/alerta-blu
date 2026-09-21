using System.Globalization;

namespace AlertaBlu.Domain.Common;

/// <summary>
/// Number and date conventions used by the Blumenau Civil Defense site (comma decimal
/// separator, <c>dd/MM/yyyy</c> dates) and by this app's Portuguese UI.
/// </summary>
/// <remarks>
/// The formats are built explicitly rather than looked up via <see cref="CultureInfo"/> so that
/// parsing stays deterministic even when the app runs with globalization-invariant mode enabled
/// (a common outcome of trimming/AOT on mobile), where <c>new CultureInfo("pt-BR")</c> silently
/// degrades to invariant behaviour and would make "2,25" parse as 225.
/// </remarks>
public static class PtBr
{
    /// <summary>Comma as decimal separator, dot as group separator.</summary>
    public static readonly NumberFormatInfo Number = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
        NumberDecimalDigits = 2,
    };

    /// <summary>Blumenau is UTC-3 all year round: Brazil abolished daylight saving time in 2019.</summary>
    public static readonly TimeSpan UtcOffset = TimeSpan.FromHours(-3);

    private static readonly string[] WeekdayAbbreviations =
        ["dom", "seg", "ter", "qua", "qui", "sex", "sáb"];

    /// <summary>Parses a Brazilian-formatted decimal such as "2,25" or "1.234,5".</summary>
    public static bool TryParseDecimal(string? text, out double value) =>
        double.TryParse(
            text?.Trim(),
            NumberStyles.Float | NumberStyles.AllowThousands,
            Number,
            out value);

    /// <summary>Parses a "dd/MM/yyyy HH:mm" timestamp as published by the site (already local time).</summary>
    public static bool TryParseDateTime(string? text, out DateTime value) =>
        DateTime.TryParseExact(
            text?.Trim(),
            ["dd/MM/yyyy HH:mm", "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out value);

    /// <summary>Parses a "dd/MM/yyyy" date as published by the site.</summary>
    public static bool TryParseDate(string? text, out DateOnly value)
    {
        if (DateOnly.TryParseExact(text?.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Formats a number with a comma decimal separator, e.g. <c>2,25</c>.</summary>
    public static string Format(double value, int decimals) =>
        value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), Number);

    /// <summary>Short Portuguese weekday label, e.g. <c>qui</c>.</summary>
    public static string Weekday(DayOfWeek day) => WeekdayAbbreviations[(int)day];
}
