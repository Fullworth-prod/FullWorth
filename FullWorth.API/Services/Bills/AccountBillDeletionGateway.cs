using FullWorth.API.Data;
using FullWorth.API.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.API.Services.Bills;

public sealed class AccountBillDeletionGateway(
    FullWorthDbContext dbContext)
    : IAccountBillDeletionGateway
{
    public async Task ApplyDependentDataDeletionAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(
            userId);

        // Remove review/evidence dependencies before statement uploads and bank rows.
        var evidence = await dbContext.SubscriptionEvidence.Where(e => e.UserId == userId)
            .ToListAsync(cancellationToken);
        dbContext.SubscriptionEvidence.RemoveRange(evidence);
        await dbContext.SaveChangesAsync(cancellationToken);
        var reviews = await dbContext.SubscriptionReviews.Where(r => r.UserId == userId)
            .ToListAsync(cancellationToken);
        dbContext.SubscriptionReviews.RemoveRange(reviews);

        var alerts =
            await dbContext.BillAlerts
                .Where(
                    alert =>
                        alert.UserId ==
                            userId)
                .ToListAsync(
                    cancellationToken);

        dbContext.BillAlerts.RemoveRange(
            alerts);

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task ApplyRootDataDeletionAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(
            userId);

        var streams =
            await dbContext.BillStreams
                .Where(
                    stream =>
                        stream.UserId ==
                            userId)
                .ToListAsync(
                    cancellationToken);

        dbContext.BillStreams.RemoveRange(
            streams);

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }

    private static void ValidateUserId(
        Guid userId)
    {
        if (userId ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "User ID is required.",
                nameof(userId));
        }
    }
}
