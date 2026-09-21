using AlertaBlu.Domain.Common;

namespace AlertaBlu.Domain;

/// <summary>
/// Flood threshold for a single street: the river level at which that address starts to flood.
/// </summary>
public sealed record CotaEnchente
{
    private string? _searchIndex;

    public required string Logradouro { get; init; }

    public string Bairro { get; init; } = string.Empty;

    /// <summary>Threshold in metres of river level.</summary>
    public double? CotaMeters { get; init; }

    public string Observacao { get; init; } = string.Empty;

    public string CotaDisplay => CotaMeters is { } value ? $"{PtBr.Format(value, 2)}m" : "--";

    public bool HasObservacao => !string.IsNullOrWhiteSpace(Observacao);

    /// <summary>
    /// Lowercase haystack for the search box, computed once on first use. The table carries
    /// ~1900 rows and the filter re-runs on every keystroke, so this avoids allocating a
    /// lowercase copy of every street name per character typed.
    /// </summary>
    public string SearchIndex => _searchIndex ??= $"{Logradouro} {Bairro}".ToLowerInvariant();
}
