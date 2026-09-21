using System.Net;
using System.Net.Http.Headers;
using AlertaBlu.Application;
using Microsoft.Extensions.DependencyInjection;

namespace AlertaBlu.Infrastructure;

/// <summary>Composition root helper for wiring the Infrastructure layer into DI.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Per-request ceiling; the whole refresh is additionally bounded by the view model.</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Registers <see cref="IAlertaBluService"/> behind a resilient <see cref="HttpClient"/>: two
    /// retries with jitter, a circuit breaker and a per-try timeout, since every call here is an
    /// idempotent GET against a live third-party site that occasionally drops a request.
    /// </summary>
    public static IServiceCollection AddAlertaBluInfrastructure(this IServiceCollection services)
    {
        services.AddHttpClient<IAlertaBluService, AlertaBluService>(client =>
            {
                client.Timeout = RequestTimeout;
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

        return services;
    }
}
