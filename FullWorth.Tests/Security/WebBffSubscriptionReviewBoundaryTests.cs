using System.Net;
using System.Net.Http.Json;
using FullWorth.Core.Subscriptions;
using FullWorth.Tests.Infrastructure;

namespace FullWorth.Tests.Security;

public sealed class WebBffSubscriptionReviewBoundaryTests
{
    private const string ReviewId = "11111111-1111-1111-1111-111111111111";

    [Theory]
    [InlineData("/bff/subscription-reviews")]
    [InlineData("/bff/subscription-reviews/" + ReviewId)]
    public async Task AnonymousReads_AreDenied(string route)
    {
        using var factory = new FullWorthWebFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-FullWorth-Test-Anonymous", "true");
        using var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnonymousWrites_AreDenied(bool evidence)
    {
        using var factory = new FullWorthWebFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-FullWorth-Test-Anonymous", "true");
        using var response = await Write(client, evidence);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuthenticatedWrites_RequireAntiforgery(bool evidence)
    {
        using var factory = new FullWorthWebFactory();
        using var client = factory.CreateHttpsClient();
        using var response = await Write(client, evidence);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EmptyReviewId_IsRejectedBeforeReadForwarding()
    {
        using var factory = new FullWorthWebFactory();
        using var client = factory.CreateHttpsClient();
        using var response = await client.GetAsync(
            "/bff/subscription-reviews/00000000-0000-0000-0000-000000000000");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/bff/subscription-reviews/not-a-guid")]
    [InlineData("/bff/subscription-reviews/not-a-guid/evidence")]
    public async Task MalformedIdentifiers_DoNotMatchSensitiveRoutes(string route)
    {
        using var factory = new FullWorthWebFactory();
        using var client = factory.CreateHttpsClient();
        using var response = route.EndsWith("/evidence", StringComparison.Ordinal)
            ? await client.PostAsJsonAsync(route, NewEvidence())
            : await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static Task<HttpResponseMessage> Write(HttpClient client, bool evidence) =>
        evidence
            ? client.PostAsJsonAsync("/bff/subscription-reviews/" + ReviewId + "/evidence", NewEvidence())
            : client.PostAsJsonAsync("/bff/subscription-reviews",
                new CreateSubscriptionReviewRequest(Guid.NewGuid(), "Synthetic", "Personal", "Direct"));

    private static SubscriptionEvidenceRequest NewEvidence() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "UserConfirmed", null,
            [new("Renewal", "No", DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1))]);
}
