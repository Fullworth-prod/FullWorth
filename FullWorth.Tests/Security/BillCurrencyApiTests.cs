using System.Net;
using System.Net.Http.Json;
using FullWorth.API.Controllers;
using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.Core.Models;
using FullWorth.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FullWorth.Tests.Security;

public sealed class BillCurrencyApiTests
{
    [Theory]
    [InlineData("EUR", "EUR", true, "EUR")]
    [InlineData("USD", "EUR", false, null)]
    [InlineData("USD", null, false, null)]
    public async Task ListAndDetail_PreserveUnits_AndHideIncompatibleComparisons(
        string first, string? second, bool hasEvidence, string? expected)
    {
        await using var factory = new FullWorthApiFactory();
        using var client = factory.CreateHttpsClient();
        var user = await TestUserAuthentication.RegisterAndLoginAsync(client);
        var owner = await TestUserAuthentication.GetUserIdAsync(factory, user.Email);
        TestUserAuthentication.Authorize(client, user);
        var id = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FullWorthDbContext>();
            db.BillStreams.Add(new BillStreamEntity { Id = id, UserId = owner,
                ProviderName = "Synthetic currency bill", IsActive = true, Source = BillStreamSource.Manual });
            for (var month = 1; month <= 2; month++)
            {
                var transaction = new BankTransactionEntity { UserId = owner, BankAccountId = Guid.NewGuid(),
                    PlaidTransactionId = Guid.NewGuid().ToString("N"), Name = "Synthetic currency bill",
                    Amount = month * 10m, IsoCurrencyCode = month == 1 ? first : second,
                    PostedDate = new DateOnly(2026, month, 5) };
                db.BankTransactions.Add(transaction);
                db.BillTransactionAssociations.Add(new BillTransactionAssociationEntity
                    { UserId = owner, BankTransactionId = transaction.Id, BillStreamId = id });
            }
            var statement = new BillStatementEntity { UserId = owner, BillStreamId = id,
                PeriodStart = new DateOnly(2026, 1, 1), PeriodEnd = new DateOnly(2026, 1, 31),
                TotalAmount = 10m, CurrencyCode = "EUR" };
            db.BillStatements.Add(statement);
            db.BillChanges.Add(new BillChangeEntity { UserId = owner, BillStreamId = id,
                PreviousStatementId = statement.Id, CurrentStatementId = statement.Id,
                Description = "Synthetic comparison", PreviousAmount = 10m, CurrentAmount = 20m,
                AmountDifference = 10m, AnnualizedImpact = 120m });
            await db.SaveChangesAsync();
        }
        var list = await client.GetFromJsonAsync<List<BillStreamResult>>("/api/bill-streams");
        var item = Assert.Single(Assert.IsType<List<BillStreamResult>>(list));
        Assert.Equal(expected, item.CurrencyCode);
        Assert.Equal(hasEvidence, item.HasTransactionEvidence);
        Assert.Equal(hasEvidence, item.HasMonthlyCadenceEvidence);
        var detail = await client.GetFromJsonAsync<BillStreamDetailResult>($"/api/bill-streams/{id}");
        Assert.NotNull(detail);
        Assert.Equal(expected, detail.CurrencyCode);
        Assert.Equal(hasEvidence, detail.HasTransactionEvidence);
        Assert.Equal(hasEvidence ? 10m : 0m, detail.PreviousAverage);
        Assert.Equal("EUR", Assert.Single(detail.Statements).CurrencyCode);
        Assert.Equal("EUR", Assert.Single(detail.Changes).CurrencyCode);

        using var otherClient = factory.CreateHttpsClient();
        var other = await TestUserAuthentication.RegisterAndLoginAsync(otherClient);
        TestUserAuthentication.Authorize(otherClient, other);
        using var foreign = await otherClient.GetAsync($"/api/bill-streams/{id}");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Empty(Assert.IsType<List<BillStreamResult>>(
            await otherClient.GetFromJsonAsync<List<BillStreamResult>>("/api/bill-streams")));
    }
}
