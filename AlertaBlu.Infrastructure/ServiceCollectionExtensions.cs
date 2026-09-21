using System.Net;
using System.Net.Http.Headers;
using AlertaBlu.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlertaBlu.Infrastructure;

/// <summary>Composition root helper for wiring the Infrastructure layer into DI.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IAlertaBluGateway"/> behind a resilient <see cref="HttpClient"/>: two
    /// retries with jitter, a circuit breaker and a per-try timeout, since every call here is an
    /// idempotent GET against a live third-party site that occasionally drops a request. Also
    /// registers <see cref="AlertaBluOptions"/> (bound from <paramref name="configuration"/>, with
    /// code defaults for anything not overridden), the offline cache and clock backing
    /// <see cref="LoadDashboardUseCase"/>, and the use case itself.
    /// </summary>
    /// <param name="cacheDirectory">
    /// A writable, app-private directory (e.g. MAUI's <c>FileSystem.AppDataDirectory</c>). This
    /// project stays free of platform storage APIs, so the caller supplies the path.
    /// </param>
    /// <param name="configuration">
    /// Configuration to bind <see cref="AlertaBluOptions"/> from (section <see cref="AlertaBluOptions.SectionName"/>).
    /// An empty/default configuration is fine: every option already has a working default.
    /// </param>
    public static IServiceCollection AddAlertaBluInfrastructure(
        this IServiceCollection services, string cacheDirectory, IConfiguration configuration)
    {
        services.Configure<AlertaBluOptions>(configuration.GetSection(AlertaBluOptions.SectionName));
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<AlertaBluOptions>>().Value);

        services.AddSingleton<IClock, SystemClock>();

        services.AddSingleton<IDashboardCache>(provider => new FileDashboardCache(
            Path.Combine(cacheDirectory, "dashboard-cache.json"),
            provider.GetRequiredService<ILogger<FileDashboardCache>>()));

        services.AddHttpClient<IAlertaBluGateway, AlertaBluService>((provider, client) =>
            {
                var options = provider.GetRequiredService<AlertaBluOptions>();
                client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
                client.DefaultRequestHeaders.UserAgent.Add(
                    new ProductInfoHeaderValue("AlertaBluApp", "1.0"));
            })
            .ConfigurePrimaryHttpMessageHandler(static () => new HttpClientHandler
            {
                // The managed handler defaults to no automatic decompression, unlike the native
                // Android/iOS handlers; without this, /p/cotas (~700 KB) is fetched uncompressed
                // on Windows.
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Brotli,
            })
            .AddStandardResilienceHandler();

        services.AddSingleton<LoadDashboardUseCase>();

        return services;
    }
}
