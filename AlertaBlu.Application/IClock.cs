namespace AlertaBlu.Application;

/// <summary>Testable seam over the wall clock, so "now"-dependent logic (staleness, TTLs) is deterministic in tests.</summary>
public interface IClock
{
    DateTimeOffset Now { get; }
}
