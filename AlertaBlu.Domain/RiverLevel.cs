using AlertaBlu.Domain.Common;

namespace AlertaBlu.Domain;

/// <summary>Direction of the last river level change, taken from the arrow icon in the table.</summary>
public enum RiverTrend
{
    Unknown,
    Rising,
    Falling,
}

/// <summary>Latest reading of the Itajaí-Açu river level at Blumenau.</summary>
public sealed record RiverLevel
{
    public required double LevelMeters { get; init; }

    /// <summary>Local timestamp of the reading, as published (already UTC-3).</summary>
    public DateTime? ReadingTime { get; init; }

    public RiverTrend Trend { get; init; } = RiverTrend.Unknown;

    /// <summary>Absolute change since the previous reading, in metres.</summary>
    public double? DeltaMeters { get; init; }

    public string LevelDisplay => $"{PtBr.Format(LevelMeters, 2)}m";

    /// <summary>Headline form with a separating space, e.g. <c>3,01 m</c>.</summary>
    public string LevelHeadline => $"{PtBr.Format(LevelMeters, 2)} m";

    public string TimeDisplay => ReadingTime?.ToString("HH:mm") ?? "--:--";

    public string DateDisplay => ReadingTime?.ToString("dd/MM") ?? string.Empty;

    public string TrendGlyph => Trend switch
    {
        RiverTrend.Rising => "▲",
        RiverTrend.Falling => "▼",
        _ => "•",
    };

    public string TrendDescription => Trend switch
    {
        RiverTrend.Rising => "subindo",
        RiverTrend.Falling => "descendo",
        _ => "estável",
    };

    /// <summary>Sentence-cased trend for the headline, e.g. <c>Subindo</c>.</summary>
    public string TrendTitle => Trend switch
    {
        RiverTrend.Rising => "Subindo",
        RiverTrend.Falling => "Descendo",
        _ => "Estável",
    };

    /// <summary>Combined trend label, e.g. <c>descendo 0,04m</c>.</summary>
    public string TrendDisplay => DeltaMeters is { } delta
        ? $"{TrendDescription} {PtBr.Format(delta, 2)}m"
        : TrendDescription;
}
