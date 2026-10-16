using System.Net;
using System.Net.Http.Json;
using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Bills;
using FullWorth.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FullWorth.Tests.Security;

public sealed class BillReviewWorkflowTests
{
    [Fact]
    public async Task RejectedAutomaticBill_IsPrivatelyReviewable_AndExplicitChoiceSurvivesRescan()
    {
        await using var factory = new FullWorthApiFactory();
        using var client = factory.CreateHttpsClient();
        var user = await TestUserAuthentication.RegisterAndLoginAsync(client);
        var other = await TestUserAuthentication.RegisterAndLoginAsync(client);
        var ownerId = await TestUserAuthentication.GetUserIdAsync(factory, user.Email);
        var otherId = await TestUserAuthentication.GetUserIdAsync(factory, other.Email);
        Guid id;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FullWorthDbContext>();
            var stream = new BillStreamEntity
            {
                UserId = ownerId, ProviderName = "Synthetic review bill",
                Source = BillStreamSource.AutomaticDiscovery, IsActive = false
            };
            id = stream.Id;
            db.BillStreams.Add(stream);
            db.BillStreams.Add(new BillStreamEntity
            {
                UserId = otherId, ProviderName = "Other owner's private bill",
                Source = BillStreamSource.AutomaticDiscovery, IsActive = false
            });
            await db.SaveChangesAsync();
        }
        TestUserAuthentication.Authorize(client, user);
        Assert.Empty((await client.GetFromJsonAsync<List<Payload>>("/api/bill-streams"))!);
        var review = Assert.Single((await client.GetFromJsonAsync<List<Payload>>(
            "/api/bill-streams?includeInactive=true"))!);
        Assert.Equal(id, review.Id);
        Assert.Equal("AutomaticDiscovery", review.Source);
        Assert.False(review.IsActive);
        Assert.False(review.HasTransactionEvidence);
        Assert.False(review.HasMonthlyCadenceEvidence);
        var detail = await client.GetFromJsonAsync<Payload>($"/api/bill-streams/{id}");
        Assert.NotNull(detail);
        Assert.False(detail.IsActive);
        Assert.False(detail.HasTransactionEvidence);

        using var confirm = await client.PostAsJsonAsync("/api/bill-streams",
            new { providerName = "Synthetic review bill", category = "Utility" });
        confirm.EnsureSuccessStatusCode();
        Assert.Equal(id, (await confirm.Content.ReadFromJsonAsync<Payload>())!.Id);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<RecurringBillDiscoveryPersistenceService>()
                .DiscoverAndSaveAsync(ownerId);
            Assert.Equal(0, result.BillsDiscovered);
            Assert.Equal(0, result.BillStreamsDeactivated);
            var db = scope.ServiceProvider.GetRequiredService<FullWorthDbContext>();
            var foreign = await db.BillStreams.SingleAsync(s => s.UserId == otherId);
            Assert.False(foreign.IsActive);
            Assert.Equal(BillStreamSource.AutomaticDiscovery, foreign.Source);
        }
        var kept = Assert.Single((await client.GetFromJsonAsync<List<Payload>>("/api/bill-streams"))!);
        Assert.Equal(id, kept.Id);
        Assert.Equal("Manual", kept.Source);
        Assert.True(kept.IsActive);
        Assert.False(kept.HasTransactionEvidence);
        TestUserAuthentication.Authorize(client, other);
        using var denied = await client.GetAsync($"/api/bill-streams/{id}");
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }

    [Theory]
    [InlineData("999")]
    [InlineData("-1")]
    [InlineData("1")]
    [InlineData("Internet, MobilePhone")]
    public async Task Create_RejectsNumericAndCompositeCategoryValues(string category)
    {
        await using var factory = new FullWorthApiFactory();
        using var client = factory.CreateHttpsClient();
        var user = await TestUserAuthentication.RegisterAndLoginAsync(client);
        TestUserAuthentication.Authorize(client, user);
        using var response = await client.PostAsJsonAsync("/api/bill-streams",
            new { providerName = "Synthetic provider", category });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BrowserConfirmation_RequiresAuthenticationAndAntiforgery()
    {
        using var factory = new FullWorthWebFactory();
        using var anonymous = factory.CreateHttpsClient();
        anonymous.DefaultRequestHeaders.Add("X-FullWorth-Test-Anonymous", "true");
        using var denied = await anonymous.PostAsJsonAsync("/bff/bill-streams",
            new { providerName = "Synthetic provider", category = "Utility" });
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using var client = factory.CreateHttpsClient();
        using var csrf = await client.PostAsJsonAsync("/bff/bill-streams",
            new { providerName = "Synthetic provider", category = "Utility" });
        Assert.Equal(HttpStatusCode.BadRequest, csrf.StatusCode);
        using var privateList = await anonymous.GetAsync("/bff/bill-streams?includeInactive=true");
        Assert.Equal(HttpStatusCode.Unauthorized, privateList.StatusCode);
    }

    [Fact]
    public async Task BrowserConfirmation_WithCsrfButNoServerApiSession_FailsClosed()
    {
        using var factory = new FullWorthWebFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-FullWorth-Test-UserId", Guid.NewGuid().ToString("D"));
        using var tokenResponse = await client.GetAsync("/bff/antiforgery");
        tokenResponse.EnsureSuccessStatusCode();
        var token = await tokenResponse.Content.ReadFromJsonAsync<TokenPayload>();
        Assert.NotNull(token);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.RequestToken);
        using var response = await client.PostAsJsonAsync("/bff/bill-streams",
            new { providerName = "Synthetic provider", category = "Utility" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? "");
    }

    private sealed record TokenPayload(string RequestToken);
    private sealed record Payload(Guid Id, string Source, bool IsActive,
        bool HasTransactionEvidence, bool HasMonthlyCadenceEvidence);
}
