using AlertaBlu.Domain;
using AlertaBlu.Domain.Common;

namespace AlertaBlu.ViewModels;

/// <summary>
/// One official river-level band row, wrapping the immutable <see cref="RiverThreshold"/> with its
/// display formatting. Never mutates after construction (the whole list is replaced wholesale on
/// every refresh), so no change notification is needed here.
/// </summary>
public sealed class RiverThresholdViewModel(RiverThreshold data)
{
    public RiverThreshold Data { get; } = data;

    public string Label => Data.Label;

    public bool IsCurrent => Data.IsCurrent;

    public string RangeDisplay => Data.MaxMeters is { } max
        ? Data.MinMeters <= 0d
            ? $"até {PtBr.Format(max, 2)} m"
            : $"{PtBr.Format(Data.MinMeters, 2)} – {PtBr.Format(max, 2)} m"
        : $"acima de {PtBr.Format(Data.MinMeters, 2)} m";
}
