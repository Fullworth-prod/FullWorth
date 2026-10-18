using FullWorth.Web.Components.Pages.App;
using FullWorth.Web.Presentation;

namespace FullWorth.Tests.Web;

public sealed class ActivityFeedPresentationTests
{
    [Theory]
    [InlineData("Warning", true)]
    [InlineData("CRITICAL", true)]
    [InlineData("Info", false)]
    [InlineData(null, false)]
    [InlineData("unknown", false)]
    public void AttentionClassificationIsExplicit(string? severity, bool expected) =>
        Assert.Equal(expected, ActivityFeedPresentation.IsAttentionSeverity(severity));

    [Theory]
    [InlineData("WARNING", "warning")]
    [InlineData("Critical", "critical")]
    [InlineData(null, "info")]
    [InlineData("critical injected-class", "info")]
    public void SeverityCssIsAllowlisted(string? severity, string expected) =>
        Assert.Equal(expected, ActivityFeedPresentation.SeverityClass(severity));

    [Fact]
    public void SearchCombinesWithUnreadAndAttentionWithoutMutatingOrReordering()
    {
        var a = new Activity.BillAlertViewModel { Title = "Café bill", Message = "Monthly change", Severity = "Warning" };
        var b = new Activity.BillAlertViewModel { Title = "Other", Message = "Café refund", Severity = "Info", IsRead = true };
        var dismissed = new Activity.BillAlertViewModel { Title = "Café hidden", Severity = "Critical", IsDismissed = true };
        var alerts = new[] { a, b, dismissed };
        Assert.Equal(new[] { a, b }, ActivityFeedPresentation.Filter(alerts, Activity.ActivityFilter.All, " CAFÉ "));
        Assert.Equal(new[] { a }, ActivityFeedPresentation.Filter(alerts, Activity.ActivityFilter.Unread, "café"));
        Assert.Equal(new[] { a }, ActivityFeedPresentation.Filter(alerts, Activity.ActivityFilter.Attention, "change"));
        Assert.Empty(ActivityFeedPresentation.Filter(alerts, Activity.ActivityFilter.All, "<script>"));
        Assert.False(a.IsRead);
        Assert.True(b.IsRead);
        Assert.True(dismissed.IsDismissed);
    }

    [Fact]
    public void MissingSearchFieldsAreSafe()
    {
        var alert = new Activity.BillAlertViewModel { Title = null!, Message = null! };
        Assert.Single(ActivityFeedPresentation.Filter([alert], Activity.ActivityFilter.All, null));
        Assert.Empty(ActivityFeedPresentation.Filter([alert], Activity.ActivityFilter.All, "x"));
    }

    [Fact]
    public void ReviewDestinationsUseOnlyTypedBillIdOrKnownConnectionRoute()
    {
        var id = Guid.NewGuid();
        Assert.Equal($"/app/bills/{id:D}", ActivityFeedPresentation.GetReviewDestination(id, "anything"));
        Assert.Equal("/app/account", ActivityFeedPresentation.GetReviewDestination(null, "connectionissue"));
        Assert.Null(ActivityFeedPresentation.GetReviewDestination(Guid.Empty, "BillIncrease"));
        Assert.Null(ActivityFeedPresentation.GetReviewDestination(null, "https://evil.example"));
    }
}
