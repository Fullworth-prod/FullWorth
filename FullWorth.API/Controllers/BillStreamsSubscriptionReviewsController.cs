using System.Text.Json;
using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Contracts;
using FullWorth.Core.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.API.Controllers;

[ApiController]
[Route("api/subscription-reviews")]
[Authorize]
public sealed class BillStreamsSubscriptionReviewsController(
    FullWorthDbContext dbContext, UserManager<ApplicationUser> userManager,
    ISubscriptionTransactionEvidenceGateway transactionEvidence,
    ISubscriptionDocumentEvidenceGateway documentEvidence) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!TryUser(out var userId)) return Unauthorized();
        var rows = await dbContext.SubscriptionReviews.AsNoTracking()
            .Where(r => r.UserId == userId).OrderByDescending(r => r.CreatedAtUtc)
            .ThenBy(r => r.Id).Take(101)
            .Select(r => new { r.Id, r.MerchantName, r.PlanLabel, r.BillingParty })
            .ToListAsync(cancellationToken);
        return Ok(new { reviews = rows.Take(100), hasMore = rows.Count > 100 });
    }

    [HttpGet("{reviewId:guid}")]
    public async Task<IActionResult> Detail(Guid reviewId, CancellationToken cancellationToken)
    {
        if (!TryUser(out var userId)) return Unauthorized();
        var row = await dbContext.SubscriptionReviews.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == reviewId && r.UserId == userId, cancellationToken);
        return row is null ? NotFound() : Ok(await Snapshot(row, cancellationToken));
    }

    [HttpPost]
    [EnableRateLimiting("financial-refresh")]
    public async Task<IActionResult> Create(CreateSubscriptionReviewRequest request, CancellationToken cancellationToken)
    {
        if (!TryUser(out var userId)) return Unauthorized();
        if (request.Id == Guid.Empty || !ValidLabel(request.MerchantName, 200) ||
            !ValidLabel(request.PlanLabel, 100) || !ValidLabel(request.BillingParty, 100) ||
            request.SourceTransactionId == Guid.Empty || request.BillStreamId == Guid.Empty)
            return BadRequest();
        // Names/plan labels are user declarations. Never merge on merchant name.
        if (request.SourceTransactionId is { } transactionId &&
            !await transactionEvidence.IsOwnedPostedDebitAsync(userId, transactionId, cancellationToken))
            return NotFound();
        if (request.BillStreamId is { } billId && !await dbContext.BillStreams.AsNoTracking()
            .AnyAsync(b => b.UserId == userId && b.Id == billId, cancellationToken))
            return NotFound();
        var prior = await dbContext.SubscriptionReviews.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == request.Id && r.UserId == userId, cancellationToken);
        if (prior is not null)
            return SameReview(prior, request) ? Ok(await Snapshot(prior, cancellationToken)) : Conflict();
        if (await dbContext.SubscriptionReviews.CountAsync(r => r.UserId == userId, cancellationToken) >= 100)
            return Problem(statusCode: 409, title: "Subscription review limit reached.");
        var row = new SubscriptionReviewEntity
        {
            Id = request.Id, UserId = userId, Revision = Guid.NewGuid(),
            MerchantName = request.MerchantName.Trim(), PlanLabel = request.PlanLabel.Trim(),
            BillingParty = request.BillingParty.Trim(), SourceTransactionId = request.SourceTransactionId,
            BillStreamId = request.BillStreamId, CreatedAtUtc = DateTimeOffset.UtcNow
        };
        dbContext.SubscriptionReviews.Add(row);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            dbContext.Entry(row).State = EntityState.Detached;
            return Conflict();
        }
        catch
        {
            dbContext.Entry(row).State = EntityState.Detached;
            throw;
        }
        return Ok(await Snapshot(row, cancellationToken));
    }

    [HttpPost("{reviewId:guid}/evidence")]
    [EnableRateLimiting("financial-refresh")]
    public async Task<IActionResult> AddEvidence(
        Guid reviewId, SubscriptionEvidenceRequest request, CancellationToken cancellationToken)
    {
        if (!TryUser(out var userId)) return Unauthorized();
        var row = await dbContext.SubscriptionReviews
            .SingleOrDefaultAsync(r => r.Id == reviewId && r.UserId == userId, cancellationToken);
        if (row is null) return NotFound();
        if (request.SubmissionId == Guid.Empty || request.ExpectedRevision == Guid.Empty ||
            request.Source is not ("UserConfirmed" or "DocumentConfirmed") ||
            !SubscriptionVerification.TryNormalize(request.Facts, DateTimeOffset.UtcNow, out var facts))
            return BadRequest();
        if (request.Source == "DocumentConfirmed")
        {
            if (request.SourceUploadId is not { } uploadId || row.BillStreamId is not { } billId ||
                !await documentEvidence.IsOwnedBillDocumentAsync(userId, billId, uploadId, cancellationToken))
                return NotFound();
        }
        else if (request.SourceUploadId is not null) return BadRequest();
        var json = JsonSerializer.Serialize(facts);
        var prior = await dbContext.SubscriptionEvidence.AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == request.SubmissionId && e.UserId == userId &&
                e.ReviewId == reviewId, cancellationToken);
        if (prior is not null)
            return prior.Source == request.Source && prior.SourceUploadId == request.SourceUploadId &&
                prior.FactsJson == json ? Ok(await Snapshot(row, cancellationToken)) : Conflict();
        if (row.Revision != request.ExpectedRevision) return Conflict();
        if (await dbContext.SubscriptionEvidence.CountAsync(e => e.UserId == userId &&
            e.ReviewId == reviewId, cancellationToken) >= 1000)
            return Problem(statusCode: 409, title: "Subscription evidence limit reached.");
        var evidence = new SubscriptionEvidenceEntity
        {
            Id = request.SubmissionId, UserId = userId, ReviewId = reviewId,
            Source = request.Source, SourceUploadId = request.SourceUploadId,
            FactsJson = json, RecordedAtUtc = DateTimeOffset.UtcNow
        };
        var oldRevision = row.Revision;
        row.Revision = Guid.NewGuid();
        dbContext.SubscriptionEvidence.Add(evidence);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            RestoreFailedAttempt(row, oldRevision, evidence);
            return Conflict();
        }
        catch
        {
            RestoreFailedAttempt(row, oldRevision, evidence);
            throw;
        }
        return Ok(await Snapshot(row, cancellationToken));
    }

    private void RestoreFailedAttempt(
        SubscriptionReviewEntity row, Guid revision, SubscriptionEvidenceEntity evidence)
    {
        dbContext.Entry(evidence).State = EntityState.Detached;
        row.Revision = revision;
        dbContext.Entry(row).State = EntityState.Unchanged;
    }

    private async Task<SubscriptionVerificationResult> Snapshot(
        SubscriptionReviewEntity row, CancellationToken cancellationToken)
    {
        var submissions = await dbContext.SubscriptionEvidence.AsNoTracking()
            .Where(e => e.UserId == row.UserId && e.ReviewId == row.Id)
            .ToListAsync(cancellationToken);
        var observations = submissions.SelectMany(e =>
            (JsonSerializer.Deserialize<SubscriptionFactInput[]>(e.FactsJson) ?? [])
                .Select(f => new SubscriptionObservation(e.Id, e.Source, e.SourceUploadId, e.RecordedAtUtc, f)));
        var facts = SubscriptionVerification.Resolve(observations, DateTimeOffset.UtcNow);
        return new(row.Id, row.Revision, row.MerchantName, row.PlanLabel, row.BillingParty,
            row.SourceTransactionId, row.BillStreamId,
            SubscriptionVerification.HasInconsistentClaims(facts) ||
                facts.Any(f => f.Field == "Renewal" && f.State != "Current"), facts);
    }

    private static bool SameReview(SubscriptionReviewEntity row, CreateSubscriptionReviewRequest r) =>
        row.MerchantName == r.MerchantName.Trim() && row.PlanLabel == r.PlanLabel.Trim() &&
        row.BillingParty == r.BillingParty.Trim() && row.SourceTransactionId == r.SourceTransactionId &&
        row.BillStreamId == r.BillStreamId;
    private static bool ValidLabel(string? value, int max) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max && !value.Any(char.IsControl);
    private bool TryUser(out Guid userId) =>
        Guid.TryParse(userManager.GetUserId(User), out userId) && userId != Guid.Empty;
}
