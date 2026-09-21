using System.Net;

namespace AlertaBlu.Tests;

/// <summary>
/// Serves canned responses per URL fragment so the service can be exercised without a network.
/// Any endpoint not explicitly configured returns 503, which is how "this source is down" is
/// simulated.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, string> _responses = [];

    /// <summary>When set, the handler blocks until the request token is cancelled.</summary>
    public bool BlockUntilCancelled { get; init; }

    public List<string> RequestedUrls { get; } = [];

    public StubHttpMessageHandler Respond(string urlFragment, string body)
    {
        _responses[urlFragment] = body;
        return this;
    }

    /// <summary>Configures every endpoint with a healthy payload.</summary>
    public static StubHttpMessageHandler Healthy() =>
        new StubHttpMessageHandler()
            .Respond("temperaturas.json", Fixtures.TemperaturasJson)
            .Respond("api.open-meteo.com", Fixtures.OpenMeteoJson)
            .Respond("/p/detalhada", Fixtures.DetalhadaHtml)
            .Respond("/d/nivel-do-rio", Fixtures.RiverHtml)
            .Respond("nivel_oficial.json", Fixtures.NivelOficialJson)
            .Respond("/p/cotas", Fixtures.CotasHtml)
            .Respond("/d/barragens", Fixtures.BarragensHtml);

    /// <summary>Removes an endpoint so it starts answering 503.</summary>
    public StubHttpMessageHandler Without(string urlFragment)
    {
        _responses.Remove(urlFragment);
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.ToString();

        lock (RequestedUrls)
        {
            RequestedUrls.Add(url);
        }

        if (BlockUntilCancelled)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }

        var match = _responses.FirstOrDefault(pair => url.Contains(pair.Key, StringComparison.Ordinal));

        return match.Value is null
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(match.Value) };
    }
}
