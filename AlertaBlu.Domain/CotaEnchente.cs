using AlertaBlu.Domain.Common;

namespace AlertaBlu.Domain;

/// <summary>
/// Flood threshold for a single street: the river level at which that address starts to flood.
/// </summary>
public sealed record CotaEnchente
{
    public required string Logradouro { get; init; }

    public string Bairro { get; init; } = string.Empty;

    /// <summary>Threshold in metres of river level.</summary>
    public double? CotaMeters { get; init; }

    public string Observacao { get; init; } = string.Empty;

    public string CotaDisplay => CotaMeters is { } value ? $"{PtBr.Format(value, 2)}m" : "--";

    public bool HasObservacao => !string.IsNullOrWhiteSpace(Observacao);
}
