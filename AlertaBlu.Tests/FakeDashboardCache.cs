using AlertaBlu.Application;
using AlertaBlu.Domain;

namespace AlertaBlu.Tests;

/// <summary>In-memory <see cref="IDashboardCache"/> double: no disk I/O, seed via <see cref="Seeded"/>.</summary>
internal sealed class FakeDashboardCache : IDashboardCache
{
    public DashboardSnapshot? Seeded { get; set; }

    public DashboardSnapshot? LastWritten { get; private set; }

    public Task<DashboardSnapshot?> ReadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Seeded);

    public Task WriteAsync(DashboardSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        LastWritten = snapshot;
        return Task.CompletedTask;
    }
}
