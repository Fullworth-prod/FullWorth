using System.Reflection;
using FullWorth.Web.Components.Pages.App;
using FullWorth.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace FullWorth.Tests.Web;

public sealed class ExperienceSetupLifecycleTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private sealed class Module : IJSObjectReference
    {
        public Func<string, Task<object?>> Response { get; set; } = _ => Task.FromResult<object?>(Valid());
        public List<string> Calls { get; } = [];
        public bool Disposed { get; private set; }
        public ValueTask<T> InvokeAsync<T>(string name, object?[]? args) => InvokeAsync<T>(name, CancellationToken.None, args);
        public async ValueTask<T> InvokeAsync<T>(string name, CancellationToken token, object?[]? args)
        {
            Calls.Add(name);
            return (T)(await Response(name))!;
        }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
    private sealed class Text : IStringLocalizer<ExperienceSetup>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] args] => new(name, string.Format(name, args));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
    private sealed class Navigation : NavigationManager
    {
        public List<string> Destinations { get; } = [];
        public Navigation() => Initialize("https://localhost/", "https://localhost/app/setup");
        protected override void NavigateToCore(string uri, bool forceLoad) => Destinations.Add(uri);
        protected override void NavigateToCore(string uri, NavigationOptions options) => Destinations.Add(uri);
    }
    private sealed class Runtime(Task<IJSObjectReference> pending) : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string name, object?[]? args) => InvokeAsync<T>(name, CancellationToken.None, args);
        public async ValueTask<T> InvokeAsync<T>(string name, CancellationToken token, object?[]? args) => (T)(object)await pending;
    }
    private static ExperienceSetupResponse Valid(bool complete = true) => new()
    {
        ExperienceSetupComplete = complete, PreferredUiLanguage = "en-US", ThemePreference = "Dark",
        TextSizePreference = "Large", HighContrastEnabled = true, ReduceMotionEnabled = true,
        ExperienceFocus = ["BillChanges", "Statements"]
    };
    private static ExperienceSetup Page(Module module, Navigation? nav = null)
    {
        var page = new ExperienceSetup();
        Set(page, "_preferencesModule", module);
        typeof(ExperienceSetup).GetProperty("L", Flags)!.SetValue(page, new Text());
        typeof(ExperienceSetup).GetProperty("Navigation", Flags)!.SetValue(page, nav ?? new Navigation());
        return page;
    }
    private static void Set(ExperienceSetup page, string name, object? value) => typeof(ExperienceSetup).GetField(name, Flags)!.SetValue(page, value);
    private static T Get<T>(ExperienceSetup page, string name) => (T)typeof(ExperienceSetup).GetField(name, Flags)!.GetValue(page)!;
    private static T Property<T>(ExperienceSetup page, string name) => (T)typeof(ExperienceSetup).GetProperty(name, Flags)!.GetValue(page)!;
    private static Task Run(ExperienceSetup page, string name) => (Task)typeof(ExperienceSetup).GetMethod(name, Flags)!.Invoke(page, null)!;
    private static void Act(ExperienceSetup page, string name, params object?[] args) => typeof(ExperienceSetup).GetMethod(name, Flags)!.Invoke(page, args);

    [Theory]
    [InlineData("null")]
    [InlineData("missing")]
    [InlineData("theme")]
    [InlineData("focus")]
    public void InvalidResponse_RejectsRatherThanManufacturingDefaults(string kind)
    {
        var response = Valid();
        if (kind == "null") response = null!;
        else if (kind == "missing") response.ReduceMotionEnabled = null;
        else if (kind == "theme") response.ThemePreference = "new-unknown-theme";
        else response.ExperienceFocus = ["BillChanges", "BillChanges"];
        Assert.Throws<InvalidOperationException>(() => ExperienceSetupValues.Read(response));
    }

    [Fact]
    public void Response_NormalizesSupportedValuesAndCopiesFocus()
    {
        var response = Valid();
        response.PreferredUiLanguage = " ES-US ";
        response.ThemePreference = " dark ";
        response.TextSizePreference = "extra-large";
        var values = ExperienceSetupValues.Read(response, true);
        response.ExperienceFocus![0] = "Spending";
        Assert.Equal("es", values.Language);
        Assert.Equal("ExtraLarge", values.TextSize);
        Assert.Equal(new[] { "BillChanges", "Statements" }, values.Focus);
        Assert.True(values.Matches(values with { Focus = new[] { "Statements", "BillChanges" } }));
        Assert.False(values.Matches(values with { HighContrast = false }));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EmptyFocus_IsOnlyValidForIncompleteAccounts(bool complete)
    {
        var response = Valid(complete);
        response.ExperienceFocus = [];
        if (complete) Assert.Throws<InvalidOperationException>(() => ExperienceSetupValues.Read(response));
        else Assert.Empty(ExperienceSetupValues.Read(response).Focus);
    }

    [Fact]
    public async Task FailedInitialRead_BlocksEditingAndSaveUntilSuccessfulRetry()
    {
        var module = new Module { Response = _ => Task.FromException<object?>(new JSException("unavailable")) };
        var page = Page(module);
        await Run(page, "LoadAsync");
        Act(page, "GoToStep", 4);
        await Run(page, "FinishAsync");
        Assert.False(Get<bool>(page, "_hasLoaded"));
        Assert.Equal(1, Get<int>(page, "_step"));
        Assert.DoesNotContain("persistExperiencePreferences", module.Calls);
        module.Response = _ => Task.FromResult<object?>(Valid());
        await Run(page, "LoadAsync");
        Assert.True(Get<bool>(page, "_hasLoaded"));
        Assert.Equal("Large", Get<string>(page, "_textSize"));
        await page.DisposeAsync();
    }

    [Fact]
    public async Task Save_RequiresReviewStepAndAtLeastOneKnownPriority()
    {
        var module = new Module();
        var page = Page(module);
        await Run(page, "LoadAsync");
        await Run(page, "FinishAsync");
        Act(page, "GoToStep", 4);
        Get<HashSet<string>>(page, "_focus").Clear();
        await Run(page, "FinishAsync");
        Assert.DoesNotContain("persistExperiencePreferences", module.Calls);
        Assert.Contains("Choose at least one", Get<string>(page, "_error"));
        await page.DisposeAsync();
    }

    [Theory]
    [InlineData("throw")]
    [InlineData("null")]
    [InlineData("mismatch")]
    [InlineData("incomplete")]
    public async Task UncertainSave_KeepsDraftBlocksReplayAndRequiresRead(string outcome)
    {
        var module = new Module();
        var page = Page(module);
        await Run(page, "LoadAsync");
        Act(page, "GoToStep", 4);
        var before = Property<ExperienceSetupValues>(page, "Draft");
        module.Response = _ =>
        {
            if (outcome == "throw") return Task.FromException<object?>(new JSException("response lost"));
            if (outcome == "null") return Task.FromResult<object?>(null);
            var result = Valid(outcome != "incomplete");
            if (outcome == "mismatch") result.ThemePreference = "Light";
            return Task.FromResult<object?>(result);
        };
        await Run(page, "FinishAsync");
        await Run(page, "FinishAsync");
        Act(page, "NextStep");
        Assert.True(Get<bool>(page, "_saveUncertain"));
        Assert.True(before.Matches(Property<ExperienceSetupValues>(page, "Draft")));
        Assert.Equal(1, module.Calls.Count(name => name == "persistExperiencePreferences"));
        module.Response = _ => Task.FromException<object?>(new JSException("read unavailable"));
        await Run(page, "CheckSavedAsync");
        Assert.True(Get<bool>(page, "_saveUncertain"));
        Assert.True(before.Matches(Property<ExperienceSetupValues>(page, "Draft")));
        await page.DisposeAsync();
    }

    [Fact]
    public async Task VerificationOfMatchingSave_NavigatesWithoutSecondPut()
    {
        var module = new Module();
        var nav = new Navigation();
        var page = Page(module, nav);
        await Run(page, "LoadAsync");
        Act(page, "GoToStep", 4);
        module.Response = _ => Task.FromException<object?>(new JSException("lost"));
        await Run(page, "FinishAsync");
        module.Response = name => Task.FromResult<object?>(name == "applyExperiencePreferences" ? true : Valid());
        await Run(page, "CheckSavedAsync");
        Assert.True(Get<bool>(page, "_saveConfirmed"));
        Assert.Equal("/app", Assert.Single(nav.Destinations));
        Assert.Equal(1, module.Calls.Count(name => name == "persistExperiencePreferences"));
        await page.DisposeAsync();
    }

    [Fact]
    public async Task VerificationOfDifferentSave_RetainsEditsWithoutApplyingServerValues()
    {
        var module = new Module();
        var page = Page(module);
        await Run(page, "LoadAsync");
        Act(page, "GoToStep", 4);
        module.Response = _ => Task.FromException<object?>(new JSException("lost"));
        await Run(page, "FinishAsync");
        var different = Valid();
        different.ThemePreference = "Light";
        module.Response = _ => Task.FromResult<object?>(different);
        await Run(page, "CheckSavedAsync");
        Assert.False(Get<bool>(page, "_saveUncertain"));
        Assert.Equal("Dark", Get<string>(page, "_theme"));
        Assert.Contains("currently differ", Get<string>(page, "_notice"));
        Assert.DoesNotContain("applyExperiencePreferences", module.Calls);
        await page.DisposeAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConfirmedSave_LocalApplicationFailureDoesNotBecomeWriteFailure(bool throwing)
    {
        var module = new Module();
        var nav = new Navigation();
        var page = Page(module, nav);
        await Run(page, "LoadAsync");
        Act(page, "GoToStep", 4);
        module.Response = name => name == "applyExperiencePreferences" ?
            (throwing ? Task.FromException<object?>(new JSException("browser blocked")) : Task.FromResult<object?>(false)) :
            Task.FromResult<object?>(Valid());
        await Run(page, "FinishAsync");
        await Run(page, "FinishAsync");
        Assert.True(Get<bool>(page, "_saveConfirmed"));
        Assert.False(Get<bool>(page, "_saveUncertain"));
        Assert.Contains("was saved", Get<string>(page, "_notice"));
        Assert.Empty(nav.Destinations);
        Assert.Equal(1, module.Calls.Count(name => name == "persistExperiencePreferences"));
        Act(page, "ContinueAfterSave");
        Assert.Single(nav.Destinations);
        await page.DisposeAsync();
    }

    [Fact]
    public async Task PendingSave_LocksNavigationFocusChangesAndDuplicateEvents()
    {
        var module = new Module();
        var page = Page(module);
        await Run(page, "LoadAsync");
        Act(page, "GoToStep", 4);
        var delayed = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        module.Response = _ => delayed.Task;
        var pending = Run(page, "FinishAsync");
        Act(page, "PreviousStep");
        Act(page, "SetFocus", "Statements", new ChangeEventArgs { Value = false });
        await Run(page, "FinishAsync");
        Assert.Equal(4, Get<int>(page, "_step"));
        Assert.Contains("Statements", Get<HashSet<string>>(page, "_focus"));
        Assert.Equal(1, module.Calls.Count(name => name == "persistExperiencePreferences"));
        delayed.SetException(new JSException("lost"));
        await pending;
        await page.DisposeAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Disposal_RejectsLateReadOrSaveAndCannotNavigate(bool reading)
    {
        var module = new Module();
        var nav = new Navigation();
        var page = Page(module, nav);
        if (!reading) { await Run(page, "LoadAsync"); Act(page, "GoToStep", 4); }
        var delayed = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        module.Response = _ => delayed.Task;
        var pending = Run(page, reading ? "LoadAsync" : "FinishAsync");
        await page.DisposeAsync();
        delayed.SetResult(Valid());
        await pending;
        Assert.Empty(nav.Destinations);
        Assert.DoesNotContain("applyExperiencePreferences", module.Calls);
        if (reading) Assert.False(Get<bool>(page, "_hasLoaded"));
    }

    [Fact]
    public async Task Disposal_ReleasesLateModuleWithoutFetchingPreferences()
    {
        var module = new Module();
        var page = Page(module);
        Set(page, "_preferencesModule", null);
        var delayed = new TaskCompletionSource<IJSObjectReference>(TaskCreationOptions.RunContinuationsAsynchronously);
        typeof(ExperienceSetup).GetProperty("JS", Flags)!.SetValue(page, new Runtime(delayed.Task));
        var pending = Run(page, "LoadAsync");
        await page.DisposeAsync();
        delayed.SetResult(module);
        await pending;
        Assert.True(module.Disposed);
        Assert.Empty(module.Calls);
    }
}
