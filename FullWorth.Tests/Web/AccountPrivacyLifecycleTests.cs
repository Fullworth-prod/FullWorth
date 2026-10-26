using System.Reflection;
using FullWorth.Web.Components.Pages.App;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace FullWorth.Tests.Web;

public sealed class AccountPrivacyLifecycleTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string Password = "Synthetic!Proof123";
    private const string Code = "123456";

    private sealed class Module : IJSObjectReference
    {
        public Func<string, Task<object?>> Response { get; set; } = _ => Task.FromResult<object?>(true);
        public List<string> Calls { get; } = [];
        public bool Disposed { get; private set; }
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) =>
            InvokeAsync<T>(identifier, CancellationToken.None, args);
        public async ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken token, object?[]? args)
        {
            Calls.Add(identifier);
            if (identifier == "cancelAccountExport") return default!;
            return (T)(await Response(identifier))!;
        }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
    private sealed class Localizer : IStringLocalizer<AccountPrivacy>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] args] => new(name, string.Format(name, args));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
    private sealed class Navigation : NavigationManager
    {
        public List<string> Destinations { get; } = [];
        public Navigation() => Initialize("https://localhost/", "https://localhost/app/account/privacy");
        protected override void NavigateToCore(string uri, bool forceLoad) => Destinations.Add(uri);
        protected override void NavigateToCore(string uri, NavigationOptions options) => Destinations.Add(uri);
    }
    private sealed class DelayedRuntime(Task<IJSObjectReference> module) : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) =>
            InvokeAsync<T>(identifier, CancellationToken.None, args);
        public async ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken token, object?[]? args) =>
            (T)(object)await module;
    }

    private static AccountPrivacy Page(Module module, Navigation? navigation = null, bool interactiveReady = true)
    {
        var page = new AccountPrivacy();
        Set(page, "_interactiveReady", interactiveReady);
        Set(page, "_bffModule", module);
        Set(page, "_deleteModule", module);
        typeof(AccountPrivacy).GetProperty("L", Flags)!.SetValue(page, new Localizer());
        typeof(AccountPrivacy).GetProperty("Navigation", Flags)!.SetValue(page, navigation ?? new Navigation());
        return page;
    }
    private static void Set(AccountPrivacy page, string name, object? value) =>
        typeof(AccountPrivacy).GetField(name, Flags)!.SetValue(page, value);
    private static T Get<T>(AccountPrivacy page, string name) => (T)typeof(AccountPrivacy).GetField(name, Flags)!.GetValue(page)!;
    private static T Property<T>(AccountPrivacy page, string name) => (T)typeof(AccountPrivacy).GetProperty(name, Flags)!.GetValue(page)!;
    private static void Act(AccountPrivacy page, string method) =>
        typeof(AccountPrivacy).GetMethod(method, Flags)!.Invoke(page, null);
    private static Task Run(AccountPrivacy page, string method) =>
        (Task)typeof(AccountPrivacy).GetMethod(method, Flags)!.Invoke(page, null)!;
    private static void ExportProof(AccountPrivacy page)
    {
        Act(page, "ShowExportConfirmation");
        Set(page, "_exportCurrentPassword", Password);
        Set(page, "_exportTwoFactorCode", Code);
    }
    private static void DeleteProof(AccountPrivacy page)
    {
        Act(page, "ShowDeleteConfirmation");
        Set(page, "_currentPassword", Password);
        Set(page, "_twoFactorCode", Code);
        Set(page, "_deleteConfirmation", "DELETE");
    }
    private static void AssertNoProof(AccountPrivacy page)
    {
        foreach (var name in new[] { "_currentPassword", "_twoFactorCode", "_deleteConfirmation",
            "_exportCurrentPassword", "_exportTwoFactorCode" })
            Assert.Equal("", Get<string>(page, name));
    }

    [Fact]
    public async Task InitialReadiness_BlocksSensitiveCommandsEvenWithProof()
    {
        var module = new Module();
        var page = Page(module, interactiveReady: false);
        Assert.True(Property<bool>(page, "IsBusy"));
        Act(page, "ShowExportConfirmation");
        Act(page, "ShowDeleteConfirmation");
        Assert.False(Get<bool>(page, "_showExportConfirmation"));
        Assert.False(Get<bool>(page, "_showDeleteConfirmation"));
        Set(page, "_showExportConfirmation", true);
        Set(page, "_showDeleteConfirmation", true);
        Set(page, "_exportCurrentPassword", Password);
        Set(page, "_currentPassword", Password);
        Set(page, "_deleteConfirmation", "DELETE");
        Assert.False(Property<bool>(page, "CanExport"));
        Assert.False(Property<bool>(page, "CanDelete"));
        await Run(page, "ExportDataAsync");
        await Run(page, "DeleteAccountAsync");
        Assert.Empty(module.Calls);
        Set(page, "_interactiveReady", true);
        Assert.False(Property<bool>(page, "IsBusy"));
        Act(page, "ShowExportConfirmation");
        Assert.True(Get<bool>(page, "_showExportConfirmation"));
        Assert.False(Property<bool>(page, "CanExport"));
        AssertNoProof(page);
        await page.DisposeAsync();
    }

    [Fact]
    public async Task SwitchingForms_ClearsOtherProofAndRequiresFreshExplicitConfirmation()
    {
        var page = Page(new Module());
        ExportProof(page);
        Act(page, "ShowDeleteConfirmation");
        AssertNoProof(page);
        Assert.False(Get<bool>(page, "_showExportConfirmation"));
        Assert.False(Property<bool>(page, "CanDelete"));
        DeleteProof(page);
        Act(page, "ShowExportConfirmation");
        AssertNoProof(page);
        Assert.False(Get<bool>(page, "_showDeleteConfirmation"));
        Assert.False(Property<bool>(page, "CanExport"));
        await page.DisposeAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BusyOperation_BlocksOtherActionFormChangesAndDoubleSubmission(bool exporting)
    {
        var response = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var module = new Module { Response = _ => response.Task };
        var page = Page(module);
        if (exporting) ExportProof(page); else DeleteProof(page);
        var pending = Run(page, exporting ? "ExportDataAsync" : "DeleteAccountAsync");
        Act(page, "ShowExportConfirmation");
        Act(page, "ShowDeleteConfirmation");
        Act(page, "CancelExport");
        Act(page, "CancelDelete");
        await Run(page, "ExportDataAsync");
        await Run(page, "DeleteAccountAsync");
        Assert.Single(module.Calls);
        Assert.True(Property<bool>(page, "IsBusy"));
        response.SetException(new JSException("synthetic failure"));
        await pending;
        Assert.False(Property<bool>(page, "IsBusy"));
        AssertNoProof(page);
        await page.DisposeAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CancelAndDispose_ClearAllCredentialsAndConfirmation(bool exporting)
    {
        var module = new Module();
        var page = Page(module);
        if (exporting) ExportProof(page); else DeleteProof(page);
        Act(page, exporting ? "CancelExport" : "CancelDelete");
        AssertNoProof(page);
        if (exporting) ExportProof(page); else DeleteProof(page);
        await page.DisposeAsync();
        AssertNoProof(page);
        Assert.False(Property<bool>(page, "CanExport"));
        Assert.False(Property<bool>(page, "CanDelete"));
        Assert.Contains("cancelAccountExport", module.Calls);
        Assert.True(module.Disposed);
    }

    [Theory]
    [InlineData("DELETE ", false)]
    [InlineData("delete", false)]
    [InlineData("", false)]
    [InlineData("DELETE", true)]
    public async Task Delete_RequiresExactTypedConfirmation(string confirmation, bool expected)
    {
        var page = Page(new Module());
        DeleteProof(page);
        Set(page, "_deleteConfirmation", confirmation);
        Assert.Equal(expected, Property<bool>(page, "CanDelete"));
        await page.DisposeAsync();
    }

    [Theory]
    [InlineData("Current password is incorrect.", "Your current password is incorrect.")]
    [InlineData("The authenticator code is invalid.", "Enter a valid current authenticator code to delete this account.")]
    [InlineData("Staff accounts cannot be self-deleted.", "Staff roles must be removed")]
    [InlineData("A bank connection could not be revoked.", "could not safely revoke")]
    [InlineData("Stored statement preparation failed; account was not deleted.", "could not securely remove")]
    [InlineData("Too many attempts.", "Wait before trying again")]
    [InlineData("FullWorth session expired.", "Sign in again")]
    [InlineData("private bank details must never surface", "may or may not have been deleted")]
    public async Task Delete_FailureClearsProofAndUsesOnlySafeOutcomeCopy(string error, string expected)
    {
        var module = new Module { Response = _ => Task.FromException<object?>(new JSException(error)) };
        var navigation = new Navigation();
        var page = Page(module, navigation);
        DeleteProof(page);
        await Run(page, "DeleteAccountAsync");
        AssertNoProof(page);
        Assert.Contains(expected, Get<string>(page, "_message"));
        Assert.DoesNotContain("private bank details", Get<string>(page, "_message"));
        Assert.Empty(navigation.Destinations);
        Assert.False(Get<bool>(page, "_messageSucceeded"));
        Assert.False(Property<bool>(page, "CanDelete"));
        await page.DisposeAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Export_ResultMustBePositiveAndDoesNotClaimAFileWasSaved(bool result)
    {
        var module = new Module { Response = _ => Task.FromResult<object?>(result) };
        var page = Page(module);
        ExportProof(page);
        await Run(page, "ExportDataAsync");
        AssertNoProof(page);
        Assert.Equal(result, Get<bool>(page, "_messageSucceeded"));
        if (result)
        {
            Assert.Contains("Download requested", Get<string>(page, "_message"));
            Assert.Contains("confirm", Get<string>(page, "_message"));
            Assert.False(Get<bool>(page, "_showExportConfirmation"));
        }
        else Assert.Contains("could not prepare", Get<string>(page, "_message"));
        await page.DisposeAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Delete_NavigatesOnlyAfterConfirmedSuccess(bool result)
    {
        var module = new Module { Response = _ => Task.FromResult<object?>(result) };
        var navigation = new Navigation();
        var page = Page(module, navigation);
        DeleteProof(page);
        await Run(page, "DeleteAccountAsync");
        AssertNoProof(page);
        if (result) Assert.Equal("/?accountDeleted=1", Assert.Single(navigation.Destinations));
        else
        {
            Assert.Empty(navigation.Destinations);
            Assert.Contains("may or may not have been deleted", Get<string>(page, "_message"));
        }
        await page.DisposeAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LateCompletion_AfterDisposalCannotPublishSuccessOrNavigate(bool exporting)
    {
        var response = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var module = new Module { Response = _ => response.Task };
        var navigation = new Navigation();
        var page = Page(module, navigation);
        if (exporting) ExportProof(page); else DeleteProof(page);
        var pending = Run(page, exporting ? "ExportDataAsync" : "DeleteAccountAsync");
        await page.DisposeAsync();
        response.SetResult(true);
        await pending;
        AssertNoProof(page);
        Assert.Null(Get<string?>(page, "_message"));
        Assert.Empty(navigation.Destinations);
    }

    [Fact]
    public async Task LateImport_IsDisposedWithoutStartingSensitiveRequest()
    {
        var response = new TaskCompletionSource<IJSObjectReference>(TaskCreationOptions.RunContinuationsAsynchronously);
        var module = new Module();
        var page = Page(module);
        Set(page, "_bffModule", null);
        Set(page, "_deleteModule", null);
        typeof(AccountPrivacy).GetProperty("JS", Flags)!.SetValue(page, new DelayedRuntime(response.Task));
        ExportProof(page);
        var pending = Run(page, "ExportDataAsync");
        await page.DisposeAsync();
        response.SetResult(module);
        await pending;
        AssertNoProof(page);
        Assert.True(module.Disposed);
        Assert.DoesNotContain("downloadAccountExport", module.Calls);
        Assert.Null(Get<string?>(page, "_message"));
    }
}
