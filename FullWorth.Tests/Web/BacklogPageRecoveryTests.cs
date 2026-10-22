using System.Reflection;
using System.Text.Json;
using FullWorth.Web.Components.Pages.App;
using FullWorth.Web.Components.Shared;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace FullWorth.Tests.Web;

public sealed class BacklogPageRecoveryTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Theory]
    [InlineData("users", "_usersSkip")]
    [InlineData("keys", "_keysSkip")]
    [InlineData("audit", "_auditSkip")]
    public async Task AdminPagesAdvanceWithBoundedReadsAndFailedPagePreservesOffset(string dataset, string offset)
    {
        var page = new Admin();
        var module = new Module();
        Set(page, "_adminModule", module);
        Set(page, "_isLoading", false);
        Property(page, "L", new Localizer<Admin>());
        await Run(page, "ChangePageAsync", dataset, 1);
        Assert.Equal(50, Get<int>(page, offset));
        Assert.Equal(50, module.LastArguments![0]);
        Assert.Equal(50, module.LastArguments[1]);
        module.Fail = true;
        await Run(page, "ChangePageAsync", dataset, 1);
        Assert.Equal(50, Get<int>(page, offset));
        Assert.NotNull(Get<string?>(page, "_pagingError"));
        module.Fail = false;
        await Run(page, "ChangePageAsync", dataset, -1);
        Assert.Equal(0, Get<int>(page, offset));
        Assert.Null(Get<string?>(page, "_pagingError"));
        Assert.All(module.Calls, name => Assert.StartsWith("getAdmin", name));
    }

    [Fact]
    public async Task AcceptedStatementReadFailureRetainsIdAndRetryReadsWithoutAnotherUpload()
    {
        var page = new StatementUploadPanel();
        var module = new Module();
        var bill = Guid.NewGuid();
        var upload = Guid.NewGuid();
        Property(page, "BillStreamId", bill);
        Property(page, "L", new Localizer<StatementUploadPanel>());
        Set(page, "_bffModule", module);
        Set(page, "_acceptedUploadId", (Guid?)upload);
        Set(page, "_selectedFile", new StatementUploadPanel.SelectedStatementFileViewModel { Name = "synthetic.pdf", Size = 100 });
        module.Fail = true;
        await Run(page, "CheckAcceptedUploadAsync");
        Assert.Equal(upload, Get<Guid?>(page, "_acceptedUploadId"));
        Assert.False((bool)page.GetType().GetProperty("CanUpload", Flags)!.GetValue(page)!);
        Assert.Equal("Status unavailable", Get<string>(page, "_statusHeading"));
        module.Fail = false;
        module.Statement = new { id = upload, billStreamId = bill, status = "Processed" };
        await Run(page, "CheckAcceptedUploadAsync");
        Assert.Null(Get<Guid?>(page, "_acceptedUploadId"));
        Assert.Equal("success", Get<string>(page, "_state"));
        Assert.DoesNotContain("uploadBillStatement", module.Calls);
        Assert.Equal(2, module.Calls.Count(name => name == "getBillStatementUploadStatus"));
        await page.DisposeAsync();
    }

    [Fact]
    public async Task WrongStatementIdentityCannotConfirmProcessing()
    {
        var page = new StatementUploadPanel();
        var upload = Guid.NewGuid();
        var module = new Module { Statement = new { id = Guid.NewGuid(), billStreamId = Guid.NewGuid(), status = "Processed" } };
        Property(page, "BillStreamId", Guid.NewGuid());
        Property(page, "L", new Localizer<StatementUploadPanel>());
        Set(page, "_bffModule", module);
        Set(page, "_acceptedUploadId", (Guid?)upload);
        await Run(page, "CheckAcceptedUploadAsync");
        Assert.Equal(upload, Get<Guid?>(page, "_acceptedUploadId"));
        Assert.Equal("error", Get<string>(page, "_state"));
        await page.DisposeAsync();
    }

    private static Task Run(object page, string method, params object[] args) =>
        (Task)page.GetType().GetMethod(method, Flags)!.Invoke(page, args)!;
    private static void Set(object page, string name, object? value) => page.GetType().GetField(name, Flags)!.SetValue(page, value);
    private static void Property(object page, string name, object value) => page.GetType().GetProperty(name, Flags)!.SetValue(page, value);
    private static T Get<T>(object page, string name) => (T)page.GetType().GetField(name, Flags)!.GetValue(page)!;
    private sealed class Localizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] args] => new(name, string.Format(name, args));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
    private sealed class Module : IJSObjectReference
    {
        public bool Fail;
        public object? Statement;
        public List<string> Calls = [];
        public object?[]? LastArguments;
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => InvokeAsync<T>(identifier, default, args);
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken token, object?[]? args)
        {
            Calls.Add(identifier); LastArguments = args;
            if (Fail) throw new JSException("synthetic read failure");
            if (identifier == "clearStatementFileInput") return ValueTask.FromResult(default(T)!);
            object value = identifier == "getBillStatementUploadStatus" ? Statement! :
                identifier == "getAdminUsers" ? new[] { new { id = Guid.NewGuid(), email = "synthetic@example.test" } } :
                new { skip = (int)args![0]!, take = 50, totalCount = 151, items = new[] { new { id = Guid.NewGuid() } } };
            return ValueTask.FromResult(JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value),
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
