using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Bills;
using FullWorth.Core.Subscriptions;
using FullWorth.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FullWorth.Tests.Security;

public sealed class SubscriptionReviewAuthorizationTests
{
    [Fact]
    public async Task AnonymousAccess_IsDenied()
    {
        await using var factory = new FullWorthApiFactory();
        using var client = factory.CreateHttpsClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/subscription-reviews")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/subscription-reviews",
            NewReview())).StatusCode);
    }

    [Fact]
    public async Task EmptyReview_LeavesRenewalUnknownAndFinancialRowsUntouched()
    {
        await using var factory = new FullWorthApiFactory();
        using var client = await Login(factory);
        var result = await Create(client, NewReview());
        Assert.True(result.NeedsReview);
        Assert.Equal("Unavailable", result.MerchantVerification);
        Assert.All(result.Facts, f => Assert.Null(f.Value));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FullWorthDbContext>();
        Assert.Empty(await db.BillStreams.ToListAsync());
        Assert.Empty(await db.BankTransactions.ToListAsync());
        Assert.Empty(await db.SubscriptionEvidence.ToListAsync());
    }

    [Fact]
    public async Task ForeignReview_ReadWriteAndListAreOwnerScoped()
    {
        await using var factory = new FullWorthApiFactory();
        using var owner = await Login(factory);
        using var other = await Login(factory);
        var review = await Create(owner, NewReview());
        var saved = await Save(owner, review.Id, Evidence(review));
        Assert.Equal(HttpStatusCode.NotFound,
            (await other.GetAsync($"/api/subscription-reviews/{review.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await other.PostAsJsonAsync($"/api/subscription-reviews/{review.Id}/evidence", Evidence(review))).StatusCode);
        var list = await other.GetFromJsonAsync<ListResult>("/api/subscription-reviews");
        Assert.Empty(list!.Reviews);
        var unchanged = await owner.GetFromJsonAsync<SubscriptionVerificationResult>($"/api/subscription-reviews/{review.Id}");
        Assert.Equal(saved.Revision, unchanged!.Revision);
        Assert.Equal("Yes", unchanged.Facts.Single(f => f.Field == "Renewal").Value);
        Assert.Single(unchanged.Facts.Single(f => f.Field == "Renewal").Evidence);
    }

    [Theory]
    [InlineData("MerchantVerified")]
    [InlineData("RevokedConnection")]
    [InlineData("SpotifyProfile")]
    public async Task MerchantVerificationCannotBeSpoofedByUserPayload(string unsupportedSource)
    {
        await using var factory = new FullWorthApiFactory();
        using var client = await Login(factory);
        var review = await Create(client, NewReview());
        var request = Evidence(review) with { Source = unsupportedSource };
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/api/subscription-reviews/{review.Id}/evidence", request)).StatusCode);
        var unchanged = await client.GetFromJsonAsync<SubscriptionVerificationResult>($"/api/subscription-reviews/{review.Id}");
        Assert.Equal(review.Revision, unchanged!.Revision);
        Assert.All(unchanged.Facts, f => Assert.Empty(f.Evidence));
    }

    [Fact]
    public async Task ExplicitRetry_IsIdempotentAndStaleRevisionCannotOverwriteEvidence()
    {
        await using var factory = new FullWorthApiFactory();
        using var client = await Login(factory);
        var review = await Create(client, NewReview());
        var request = Evidence(review);
        var first = await Save(client, review.Id, request);
        var repeated = await Save(client, review.Id, request);
        Assert.Equal(first.Revision, repeated.Revision);
        Assert.Single(repeated.Facts.Single(f => f.Field == "Renewal").Evidence);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync($"/api/subscription-reviews/{review.Id}/evidence",
                request with { SubmissionId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync($"/api/subscription-reviews/{review.Id}/evidence",
                request with { Facts = [request.Facts[0] with { Value = "No" }] })).StatusCode);
    }

    [Fact]
    public async Task SameMerchantDifferentAccountsAndBillingParties_AreNotMerged()
    {
        await using var factory = new FullWorthApiFactory();
        using var client = await Login(factory);
        var first = await Create(client, NewReview() with { PlanLabel = "Personal", BillingParty = "Direct" });
        var second = await Create(client, NewReview() with { PlanLabel = "Household", BillingParty = "Apple" });
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, (await client.GetFromJsonAsync<ListResult>("/api/subscription-reviews"))!.Reviews.Count);
    }

    [Fact]
    public async Task OnePostedDebitCanSupportReview_PendingCreditRemovedAndForeignCannot()
    {
        await using var factory = new FullWorthApiFactory();
        using var client = await Login(factory);
        var userId = await CurrentUser(factory);
        var ids = new List<Guid>();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FullWorthDbContext>();
            foreach (var kind in new[] { "posted", "pending", "credit", "removed", "foreign" })
            {
                var transaction = new BankTransactionEntity
                {
                    Id = Guid.NewGuid(), UserId = kind == "foreign" ? Guid.NewGuid() : userId,
                    BankAccountId = Guid.NewGuid(), PlaidTransactionId = Guid.NewGuid().ToString(),
                    Name = "Synthetic subscription", Amount = kind == "credit" ? -10m : 10m,
                    IsPending = kind == "pending", IsRemoved = kind == "removed",
                    PostedDate = DateOnly.FromDateTime(DateTime.UtcNow)
                };
                db.BankTransactions.Add(transaction); ids.Add(transaction.Id);
            }
            await db.SaveChangesAsync();
        }
        var review = await Create(client, NewReview() with { SourceTransactionId = ids[0] });
        Assert.True(review.NeedsReview);
        foreach (var id in ids.Skip(1))
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/subscription-reviews",
                NewReview() with { SourceTransactionId = id })).StatusCode);
    }

    [Fact]
    public async Task DocumentMustBelongToExactOwnerAndLinkedBill()
    {
        await using var factory = new FullWorthApiFactory();
        using var client = await Login(factory);
        var userId = await CurrentUser(factory);
        var bill = Guid.NewGuid(); var upload = Guid.NewGuid(); var wrong = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FullWorthDbContext>();
            db.BillStreams.Add(new BillStreamEntity { Id = bill, UserId = userId, ProviderName = "Synthetic" });
            db.BillStatementUploads.Add(new BillStatementUploadEntity { Id = upload, UserId = userId, BillStreamId = bill });
            db.BillStatementUploads.Add(new BillStatementUploadEntity { Id = wrong, UserId = userId, BillStreamId = Guid.NewGuid() });
            await db.SaveChangesAsync();
        }
        var review = await Create(client, NewReview() with { BillStreamId = bill });
        var request = Evidence(review) with { Source = "DocumentConfirmed", SourceUploadId = wrong };
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync($"/api/subscription-reviews/{review.Id}/evidence", request)).StatusCode);
        var result = await Save(client, review.Id, request with { SourceUploadId = upload });
        var renewal = result.Facts.Single(f => f.Field == "Renewal");
        Assert.Equal("Yes", renewal.Value);
        Assert.Equal("DocumentConfirmed", Assert.Single(renewal.Evidence).Source);
        Assert.Equal("Unavailable", result.MerchantVerification);
    }

    [Fact]
    public async Task AccountDeletionAndExportIncludeOnlyOwnedReviewEvidence()
    {
        await using var factory = new FullWorthApiFactory();
        using var client = await Login(factory);
        var userId = await CurrentUser(factory);
        var review = await Create(client, NewReview());
        await Save(client, review.Id, Evidence(review));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FullWorthDbContext>();
        var other = new SubscriptionReviewEntity
        {
            Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Revision = Guid.NewGuid(),
            MerchantName = "Foreign", PlanLabel = "Private", BillingParty = "Private"
        };
        db.SubscriptionReviews.Add(other);
        var foreignEvidence = new SubscriptionEvidenceEntity
        {
            Id = Guid.NewGuid(), UserId = other.UserId, ReviewId = other.Id,
            Source = "UserConfirmed", RecordedAtUtc = DateTimeOffset.UtcNow,
            FactsJson = System.Text.Json.JsonSerializer.Serialize(Evidence(review).Facts)
        };
        db.SubscriptionEvidence.Add(foreignEvidence);
        await db.SaveChangesAsync();
        var export = await new AccountBillExportGateway(db).GetAsync(userId);
        Assert.Equal(review.Id, Assert.Single(export.SubscriptionReviews).Id);
        Assert.Equal(review.Id, Assert.Single(export.SubscriptionEvidence).ReviewId);
        await new AccountBillDeletionGateway(db).ApplyDependentDataDeletionAsync(userId);
        Assert.DoesNotContain(await db.SubscriptionReviews.ToListAsync(), r => r.UserId == userId);
        Assert.Empty(await db.SubscriptionEvidence.Where(e => e.UserId == userId).ToListAsync());
        Assert.True(await db.SubscriptionReviews.AnyAsync(r => r.Id == other.Id));
        Assert.True(await db.SubscriptionEvidence.AnyAsync(e => e.Id == foreignEvidence.Id && e.UserId == other.UserId));
    }

    private static CreateSubscriptionReviewRequest NewReview() =>
        new(Guid.NewGuid(), "Synthetic merchant", "Personal plan", "Direct");
    private static SubscriptionEvidenceRequest Evidence(SubscriptionVerificationResult review) =>
        new(Guid.NewGuid(), review.Revision, "UserConfirmed", null,
            [new("Renewal", "Yes", DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(10))]);
    private static async Task<SubscriptionVerificationResult> Create(HttpClient client, CreateSubscriptionReviewRequest request)
    {
        using var response = await client.PostAsJsonAsync("/api/subscription-reviews", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SubscriptionVerificationResult>())!;
    }
    private static async Task<SubscriptionVerificationResult> Save(HttpClient client, Guid id, SubscriptionEvidenceRequest request)
    {
        using var response = await client.PostAsJsonAsync($"/api/subscription-reviews/{id}/evidence", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SubscriptionVerificationResult>())!;
    }
    private static async Task<HttpClient> Login(FullWorthApiFactory factory)
    {
        var client = factory.CreateHttpsClient();
        var auth = await TestUserAuthentication.RegisterAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
    private static async Task<Guid> CurrentUser(FullWorthApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<FullWorthDbContext>().Users.Select(u => u.Id).SingleAsync();
    }
    private sealed record ListResult(List<ReviewItem> Reviews, bool HasMore);
    private sealed record ReviewItem(Guid Id, string MerchantName, string PlanLabel, string BillingParty);
}
