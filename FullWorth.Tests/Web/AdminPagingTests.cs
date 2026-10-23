using System.Reflection;
using FullWorth.Web.Components.Pages.App;

namespace FullWorth.Tests.Web;

public sealed class AdminPagingTests
{
    private static int? GetTargetSkip(int skip, int count, int? total, int direction) =>
        (int?)typeof(Admin)
            .GetMethod("GetTargetSkip", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [skip, count, total, direction]);

    [Theory]
    [InlineData(0, 50, null, 1, 50)]
    [InlineData(50, 50, null, 1, 100)]
    [InlineData(50, 49, null, 1, null)]
    [InlineData(0, 0, null, 1, null)]
    [InlineData(0, 50, 50, 1, null)]
    [InlineData(0, 50, 51, 1, 50)]
    [InlineData(50, 1, 51, -1, 0)]
    [InlineData(0, 50, 51, -1, null)]
    [InlineData(50, 50, 100, 1, null)]
    [InlineData(50, 50, 101, 1, 100)]
    public void BoundedPaginationHonorsNextPreviousEndAndUnknownUserTotals(
        int skip, int count, int? total, int direction, int? expected)
    {
        Assert.Equal(expected, GetTargetSkip(skip, count, total, direction));
    }

    [Fact]
    public void PaginationNeverOverflowsOrAcceptsArbitraryDirection()
    {
        Assert.Null(GetTargetSkip(int.MaxValue - 25, 50, null, 1));
        Assert.Null(GetTargetSkip(0, 50, null, 0));
        Assert.Null(GetTargetSkip(0, 50, null, 2));
        Assert.Equal(0, GetTargetSkip(25, 20, null, -1));
    }

    [Fact]
    public void AllThreePanelsUseIndependentReadOnlyOffsets()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "FullWorth.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);

        var source = File.ReadAllText(Path.Combine(directory.FullName,
            "FullWorth.Web", "Components", "Pages", "App", "Admin.razor"));
        foreach (var endpoint in new[]
        {
            "getAdminUsers", "getAdminAccessKeys", "getAdminAuditLog"
        })
        {
            Assert.Contains("\"" + endpoint + "\", next.Value, PageSize", source);
        }
        Assert.Contains("GetTargetSkip(_usersSkip", source);
        Assert.Contains("GetTargetSkip(_keysSkip", source);
        Assert.Contains("GetTargetSkip(_auditSkip", source);
        Assert.Contains("_paginationError", source);
        Assert.Contains("_pendingRevokeKeyId = null", source);
    }
}
