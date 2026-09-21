using AlertaBlu.Domain;

namespace AlertaBlu.Application;

/// <summary>
/// Persists the last known-good dashboard snapshot, so a network failure has something to fall
/// back to instead of a blank screen. Never throws: a corrupt or inaccessible cache is equivalent
/// to an empty one.
/// </summary>
public interface IDashboardCache
{
    /// <summary>The last snapshot written, or <see langword="null"/> if none exists or it could not be read.</summary>
    Task<DashboardSnapshot?> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists <paramref name="snapshot"/>, overwriting whatever was cached before.</summary>
    Task WriteAsync(DashboardSnapshot snapshot, CancellationToken cancellationToken = default);
}
