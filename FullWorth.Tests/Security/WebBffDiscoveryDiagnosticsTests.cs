using System.Net;
using FullWorth.Tests.Infrastructure;

namespace FullWorth.Tests.Security;

public sealed class WebBffDiscoveryDiagnosticsTests
{
    [Fact]
    public async Task AnonymousRead_IsRejected()
    {
        using var factory = new FullWorthWebFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-FullWorth-Test-Anonymous", "true");

        using var response = await client.GetAsync("/bff/bill-discovery/diagnostics");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReadWithoutServerSideApiSession_FailsClosed()
    {
        using var factory = new FullWorthWebFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync("/bff/bill-discovery/diagnostics");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? "");
    }

    [Fact]
    public async Task Diagnostics_HasNoMutationRoute()
    {
        using var factory = new FullWorthWebFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.PostAsync("/bff/bill-discovery/diagnostics", null);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }
}
