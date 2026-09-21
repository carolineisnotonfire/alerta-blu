using AlertaBlu.Application;

namespace AlertaBlu.Tests;

/// <summary>Settable <see cref="IClock"/> double, so staleness/TTL math in tests is deterministic.</summary>
internal sealed class FakeClock : IClock
{
    public DateTimeOffset Now { get; set; } = new DateTimeOffset(2026, 8, 13, 8, 0, 0, TimeSpan.FromHours(-3));
}
