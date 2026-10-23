using System.Reflection;
using FullWorth.Web.Components.Pages.App;

namespace FullWorth.Tests.Web;

public sealed class BillDetailChangeChronologyTests
{
    private static readonly BindingFlags PrivateInstance =
        BindingFlags.NonPublic | BindingFlags.Instance;

    [Fact]
    public void BackfilledOlderPeriod_DoesNotReplaceLatestBillingChange()
    {
        var january = Guid.NewGuid();
        var february = Guid.NewGuid();
        var march = Guid.NewGuid();

        var latestPeriodChange = Change(march, new DateTimeOffset(2026, 3, 3, 0, 0, 0, TimeSpan.Zero));
        var lateBackfillChange = Change(february, new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero));

        var page = Page(
            [Statement(january, new DateOnly(2026, 1, 31)),
             Statement(february, new DateOnly(2026, 2, 28)),
             Statement(march, new DateOnly(2026, 3, 31))],
            [lateBackfillChange, latestPeriodChange]);

        Assert.Equal(latestPeriodChange.Id, LatestChange(page)!.Id);
    }

    [Fact]
    public void EqualBillingPeriod_PrefersMostRecentDetection()
    {
        var statementId = Guid.NewGuid();
        var earlier = Change(statementId, new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero));
        var corrected = Change(statementId, new DateTimeOffset(2026, 4, 2, 0, 0, 0, TimeSpan.Zero));
        var page = Page([Statement(statementId, new DateOnly(2026, 3, 31))], [earlier, corrected]);

        Assert.Equal(corrected.Id, LatestChange(page)!.Id);
    }

    [Fact]
    public void OrphanedStatementReferences_NeverBecomePrimaryEvidence()
    {
        var validId = Guid.NewGuid();
        var valid = Change(validId, new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
        var orphan = Change(Guid.NewGuid(), new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero));
        var page = Page([Statement(validId, new DateOnly(2026, 3, 31))], [orphan, valid]);

        Assert.Equal(valid.Id, LatestChange(page)!.Id);
        Assert.Null(LatestChange(Page([], [orphan])));
    }

    private static BillDetail Page(
        List<BillDetail.BillStatementViewModel> statements,
        List<BillDetail.BillChangeViewModel> changes)
    {
        var page = new BillDetail();
        typeof(BillDetail).GetField("_detail", PrivateInstance)!.SetValue(
            page,
            new BillDetail.BillStreamDetailViewModel
            {
                Statements = statements,
                Changes = changes
            });
        return page;
    }

    private static BillDetail.BillStatementViewModel Statement(Guid id, DateOnly end) =>
        new() { Id = id, PeriodStart = end.AddDays(-27), PeriodEnd = end };

    private static BillDetail.BillChangeViewModel Change(Guid statementId, DateTimeOffset detected) =>
        new()
        {
            Id = Guid.NewGuid(),
            CurrentStatementId = statementId,
            DetectedAtUtc = detected
        };

    private static BillDetail.BillChangeViewModel? LatestChange(BillDetail page) =>
        (BillDetail.BillChangeViewModel?)typeof(BillDetail)
            .GetProperty("LatestChange", PrivateInstance)!.GetValue(page);
}
