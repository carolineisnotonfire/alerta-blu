using AlertaBlu.Domain.Common;

namespace AlertaBlu.Domain;

/// <summary>
/// A forecast temperature, which the site always publishes as a range
/// ("Máxima entre 21 e 23ºC") rather than a single value.
/// </summary>
public readonly record struct TemperatureRange(double Low, double High)
{
    private const double Epsilon = 0.05;

    public bool IsSingleValue => Math.Abs(High - Low) < Epsilon;

    /// <summary>Compact label for the day cards, e.g. <c>21~23°</c>.</summary>
    public string Display => IsSingleValue
        ? $"{PtBr.Format(Low, 0)}°"
        : $"{PtBr.Format(Low, 0)}~{PtBr.Format(High, 0)}°";

    public override string ToString() => Display;
}
