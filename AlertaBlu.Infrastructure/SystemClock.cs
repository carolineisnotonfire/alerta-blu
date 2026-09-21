using AlertaBlu.Application;

namespace AlertaBlu.Infrastructure;

/// <summary>Wall-clock <see cref="IClock"/>; tests substitute a fake instead.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}
