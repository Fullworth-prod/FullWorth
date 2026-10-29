using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.Core.Models;
using FullWorth.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace FullWorth.Tests.Security;

public sealed class StatementAnnualEvidenceApiTests
{
    [Theory]
    [InlineData("monthly", true)]
    [InlineData("quarterly", false)]
    [InlineData("wrong-annual", false)]
    [InlineData("zero", true)]
    public async Task Detail_QualifiesOwnedStatementEvidenceWithoutBankCadence(string mode, bool expected)
    {
        await using var factory = new FullWorthApiFactory();
        using var client = factory.CreateHttpsClient();
        using var otherClient = factory.CreateHttpsClient();
        var owner = await TestUserAuthentication.RegisterAndLoginAsync(client);
        var other = await TestUserAuthentication.RegisterAndLoginAsync(otherClient);
        var ownerId = await TestUserAuthentication.GetUserIdAsync(factory, owner.Email);
        var streamId = Guid.NewGuid();
        var previousId = Guid.NewGuid();
        var currentId = Guid.NewGuid();
        var annual = mode == "wrong-annual" ? 241m : mode == "zero" ? 0m : 240m;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FullWorthDbContext>();
            db.BillStreams.Add(new BillStreamEntity
            {
                Id = streamId, UserId = ownerId, ProviderName = "Synthetic Statement Utility",
                Category = BillCategory.Utility, Source = BillStreamSource.Manual
            });
            db.BillStatements.AddRange(
                new BillStatementEntity
                {
                    Id = previousId, UserId = ownerId, BillStreamId = streamId,
                    PeriodStart = new(2026, 1, 1),
                    PeriodEnd = mode == "quarterly" ? new(2026, 3, 31) : new(2026, 1, 31),
                    TotalAmount = 100m, CurrencyCode = "USD"
                },
                new BillStatementEntity
                {
                    Id = currentId, UserId = ownerId, BillStreamId = streamId,
                    PeriodStart = mode == "quarterly" ? new(2026, 4, 1) : new(2026, 2, 1),
                    PeriodEnd = mode == "quarterly" ? new(2026, 6, 30) : new(2026, 2, 28),
                    TotalAmount = mode == "zero" ? 100m : 120m, CurrencyCode = "USD"
                });
            db.BillChanges.Add(new BillChangeEntity
            {
                Id = Guid.NewGuid(), UserId = ownerId, BillStreamId = streamId,
                PreviousStatementId = previousId, CurrentStatementId = currentId,
                ChangeType = BillChangeType.TotalIncrease, Confidence = BillChangeConfidence.Confirmed,
                Description = "Synthetic historical comparison", PreviousAmount = 100m,
                CurrentAmount = mode == "zero" ? 100m : 120m,
                AmountDifference = mode == "zero" ? 0m : 20m, AnnualizedImpact = annual,
                DetectedAtUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }
        TestUserAuthentication.Authorize(client, owner);
        using var response = await client.GetAsync($"/api/bill-streams/{streamId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(payload.GetProperty("hasMonthlyCadenceEvidence").GetBoolean());
        var change = Assert.Single(payload.GetProperty("changes").EnumerateArray().ToArray());
        Assert.Equal(expected, change.GetProperty("hasAnnualizedImpactEvidence").GetBoolean());
        Assert.Equal(annual, change.GetProperty("annualizedImpact").GetDecimal());
        Assert.Equal("USD", change.GetProperty("currencyCode").GetString());
        Assert.Equal(2, payload.GetProperty("statements").GetArrayLength());
        TestUserAuthentication.Authorize(otherClient, other);
        using var denied = await otherClient.GetAsync($"/api/bill-streams/{streamId}");
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }
}
