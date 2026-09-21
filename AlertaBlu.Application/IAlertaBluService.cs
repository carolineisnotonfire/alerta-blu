using AlertaBlu.Domain;

namespace AlertaBlu.Application;

/// <summary>Fetches and parses everything the home screen displays.</summary>
public interface IAlertaBluService
{
    /// <summary>
    /// Loads all dashboard sections concurrently. Never throws for upstream failures: a section
    /// that could not be fetched or parsed comes back as
    /// <see cref="SectionResult{T}.Fail(string)"/> so the rest of the page still renders.
    /// </summary>
    Task<DashboardSnapshot> GetDashboardAsync(CancellationToken cancellationToken = default);
}
