using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Bills;
using FullWorth.Core.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var connection = Environment.GetEnvironmentVariable("FULLWORTH_DISPOSABLE_POSTGRES") ?? "";
var parsed = new NpgsqlConnectionStringBuilder(connection);
if (Environment.GetEnvironmentVariable("FULLWORTH_ALLOW_DISPOSABLE_POSTGRES") != "ci-fixture-only" ||
    parsed.Host is not ("127.0.0.1" or "localhost") ||
    parsed.Database != "fullworth_subscription_verification_fixture")
    throw new InvalidOperationException("Disposable loopback CI database only.");
FullWorthDbContext Db() => new(new DbContextOptionsBuilder<FullWorthDbContext>().UseNpgsql(connection).Options);
var checks = 0;
void Require(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException(label);
    checks++;
}
await using var setup = Db();
await setup.Database.MigrateAsync();
Require(!setup.Database.HasPendingModelChanges(), "Migration snapshot must match actual model.");
var owner = Guid.NewGuid(); var other = Guid.NewGuid();
setup.Users.AddRange(new ApplicationUser { Id = owner, UserName = "fixture-owner" },
    new ApplicationUser { Id = other, UserName = "fixture-other" });
var ownBill = new BillStreamEntity { UserId = owner, ProviderName = "Synthetic owner", Category = BillCategory.Other };
var foreignBill = new BillStreamEntity { UserId = other, ProviderName = "Synthetic other" };
var bank = new BankConnectionEntity { UserId = owner, InstitutionName = "Synthetic" };
var account = new BankAccountEntity { UserId = owner, BankConnectionId = bank.Id, Name = "Synthetic" };
var payment = new BankTransactionEntity { UserId = owner, BankAccountId = account.Id,
    Name = "Synthetic", PlaidTransactionId = Guid.NewGuid().ToString(), Amount = 10m,
    IsoCurrencyCode = "USD", PostedDate = DateOnly.FromDateTime(DateTime.UtcNow) };
var foreignUpload = new BillStatementUploadEntity { UserId = other, BillStreamId = foreignBill.Id,
    StorageKey = "synthetic-no-file", FileExtension = ".pdf", MediaType = "application/pdf" };
setup.BillStreams.AddRange(ownBill, foreignBill);
setup.BankConnections.Add(bank); setup.BankAccounts.Add(account); setup.BankTransactions.Add(payment);
setup.BillStatementUploads.Add(foreignUpload);
await setup.SaveChangesAsync();
SubscriptionReviewEntity Review(Guid user, Guid? bill = null, Guid? transaction = null) => new()
{
    Id = Guid.NewGuid(), UserId = user, Revision = Guid.NewGuid(),
    MerchantName = "Synthetic", PlanLabel = "Exact plan", BillingParty = "Direct",
    BillStreamId = bill, SourceTransactionId = transaction, CreatedAtUtc = DateTimeOffset.UtcNow
};
SubscriptionEvidenceEntity Evidence(Guid reviewId, Guid userId, Guid? upload = null) => new()
{
    Id = Guid.NewGuid(), UserId = userId, ReviewId = reviewId, Source = "UserConfirmed",
    SourceUploadId = upload, FactsJson = "[]", RecordedAtUtc = DateTimeOffset.UtcNow
};
async Task Reject(Func<FullWorthDbContext, Task> attempt, string label)
{
    await using var db = Db();
    try { await attempt(db); }
    catch (DbUpdateException) { checks++; return; }
    throw new InvalidOperationException(label);
}
var review = Review(owner, ownBill.Id, payment.Id);
setup.SubscriptionReviews.Add(review);
var foreignReview = Review(other, foreignBill.Id);
setup.SubscriptionReviews.Add(foreignReview);
await setup.SaveChangesAsync();
Require(await setup.BillStreams.CountAsync() == 2 && await setup.BankTransactions.CountAsync() == 1,
    "One transaction review must not create bills or ledger rows.");
await Reject(async db => { db.SubscriptionReviews.Add(Review(owner, foreignBill.Id)); await db.SaveChangesAsync(); },
    "Foreign bill must violate owner FK.");
await Reject(async db => { db.SubscriptionReviews.Add(Review(other, transaction: payment.Id)); await db.SaveChangesAsync(); },
    "Foreign payment must violate owner FK.");
await Reject(async db => { db.SubscriptionEvidence.Add(Evidence(foreignReview.Id, owner)); await db.SaveChangesAsync(); },
    "Foreign review must violate owner FK.");
await Reject(async db => { db.SubscriptionEvidence.Add(Evidence(review.Id, owner, foreignUpload.Id)); await db.SaveChangesAsync(); },
    "Foreign document must violate owner FK.");
await using (var first = Db())
await using (var second = Db())
{
    var a = await first.SubscriptionReviews.SingleAsync(r => r.Id == review.Id && r.UserId == owner);
    var b = await second.SubscriptionReviews.SingleAsync(r => r.Id == review.Id && r.UserId == owner);
    a.Revision = Guid.NewGuid(); b.Revision = Guid.NewGuid();
    var winning = Evidence(review.Id, owner); var losing = Evidence(review.Id, owner);
    first.SubscriptionEvidence.Add(winning); second.SubscriptionEvidence.Add(losing);
    await first.SaveChangesAsync();
    try { await second.SaveChangesAsync(); throw new InvalidOperationException("Stale writer succeeded."); }
    catch (DbUpdateConcurrencyException) { checks++; }
    await using var read = Db();
    Require(await read.SubscriptionEvidence.CountAsync(e => e.ReviewId == review.Id) == 1,
        "Losing submission must roll back atomically.");
    Require(!await read.SubscriptionEvidence.AnyAsync(e => e.Id == losing.Id),
        "Losing evidence must not survive the rejected revision.");
}
var duplicateId = Guid.NewGuid();
await using (var db = Db())
{
    var submission = Evidence(review.Id, owner); submission.Id = duplicateId;
    db.SubscriptionEvidence.Add(submission); await db.SaveChangesAsync();
}
await Reject(async db =>
{
    var submission = Evidence(review.Id, owner); submission.Id = duplicateId;
    db.SubscriptionEvidence.Add(submission); await db.SaveChangesAsync();
}, "Duplicate submission ID must be durable.");
await Reject(async db =>
{
    var bill = await db.BillStreams.SingleAsync(b => b.Id == ownBill.Id);
    db.BillStreams.Remove(bill); await db.SaveChangesAsync();
}, "Supporting bill must not be deleted while a review references it.");
await using (var db = Db())
{
    var export = await new AccountBillExportGateway(db).GetAsync(owner);
    Require(export.SubscriptionReviews.Count == 1 && export.SubscriptionReviews[0].Id == review.Id,
        "Export must contain only owner's reviews.");
    Require(export.SubscriptionEvidence.Count == 2, "Export must retain both owned submissions.");
    await new AccountBillDeletionGateway(db).ApplyDependentDataDeletionAsync(owner);
    Require(!await db.SubscriptionReviews.AnyAsync(r => r.UserId == owner), "Erase owner reviews.");
    Require(!await db.SubscriptionEvidence.AnyAsync(e => e.UserId == owner), "Erase owner evidence.");
    Require(await db.SubscriptionReviews.AnyAsync(r => r.Id == foreignReview.Id), "Preserve foreign reviews.");
    Require(await db.BankTransactions.CountAsync() == 1, "Review deletion cannot erase posted payments.");
}
Console.WriteLine($"Subscription verification PostgreSQL: {checks} checks passed; synthetic isolated fixture only.");
