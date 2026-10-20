using System.Reflection;
using FullWorth.Web.Components.Shared;
using Microsoft.JSInterop;

namespace FullWorth.Tests.Web;

public sealed class NavigationViewportObserverTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private sealed class Module : IJSObjectReference
    {
        public List<string> Calls { get; } = [];
        public Task<long> Lease { get; set; } = Task.FromResult(17L);
        public bool FailDisconnect { get; set; }
        public int Disposals { get; private set; }
        public ValueTask<T> InvokeAsync<T>(string name, object?[]? args) => InvokeAsync<T>(name, CancellationToken.None, args);
        public async ValueTask<T> InvokeAsync<T>(string name, CancellationToken token, object?[]? args)
        {
            Calls.Add(name);
            if (name == "observeNavigation") return (T)(object)await Lease;
            if (FailDisconnect) throw new JSDisconnectedException("Synthetic disposed browser");
            return default!;
        }
        public ValueTask DisposeAsync() { Disposals++; return ValueTask.CompletedTask; }
    }
    private sealed class Runtime(Task<IJSObjectReference> module) : IJSRuntime
    {
        public int Imports { get; private set; }
        public ValueTask<T> InvokeAsync<T>(string name, object?[]? args) => InvokeAsync<T>(name, CancellationToken.None, args);
        public async ValueTask<T> InvokeAsync<T>(string name, CancellationToken token, object?[]? args)
        {
            Assert.Equal("import", name);
            Assert.Equal("./js/navigation-viewport.js", args![0]);
            Imports++;
            return (T)(object)await module;
        }
    }
    private static NavigationViewportObserver Page(Runtime runtime)
    {
        var page = new NavigationViewportObserver();
        typeof(NavigationViewportObserver).GetProperty("JS", Flags)!.SetValue(page, runtime);
        return page;
    }
    private static Task Render(NavigationViewportObserver page, bool first = true) =>
        (Task)typeof(NavigationViewportObserver).GetMethod("OnAfterRenderAsync", Flags)!.Invoke(page, [first])!;

    [Fact]
    public async Task Observer_InitializesOnceAndDisposesItsExactLeaseOnce()
    {
        var module = new Module();
        var runtime = new Runtime(Task.FromResult<IJSObjectReference>(module));
        var page = Page(runtime);
        await Render(page);
        await Render(page, false);
        Assert.Equal(1, runtime.Imports);
        Assert.Equal(new[] { "observeNavigation" }, module.Calls);
        await page.DisposeAsync();
        await page.DisposeAsync();
        await Render(page);
        Assert.Equal(new[] { "observeNavigation", "disconnectNavigation" }, module.Calls);
        Assert.Equal(1, module.Disposals);
    }

    [Fact]
    public async Task LateImport_IsReleasedWithoutStartingAnObserver()
    {
        var module = new Module();
        var pending = new TaskCompletionSource<IJSObjectReference>(TaskCreationOptions.RunContinuationsAsynchronously);
        var page = Page(new Runtime(pending.Task));
        var render = Render(page);
        await page.DisposeAsync();
        pending.SetResult(module);
        await render;
        Assert.Empty(module.Calls);
        Assert.Equal(1, module.Disposals);
    }

    [Fact]
    public async Task LateLease_IsDisconnectedAfterComponentDisposal()
    {
        var pending = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        var module = new Module { Lease = pending.Task };
        var page = Page(new Runtime(Task.FromResult<IJSObjectReference>(module)));
        var render = Render(page);
        await page.DisposeAsync();
        pending.SetResult(28);
        await render;
        Assert.Equal(new[] { "observeNavigation", "disconnectNavigation" }, module.Calls);
        Assert.Equal(1, module.Disposals);
    }

    [Fact]
    public async Task FailedObservation_ReleasesTheModuleAndUsesCssFallback()
    {
        var module = new Module { Lease = Task.FromException<long>(new JSException("Synthetic geometry failure")) };
        var page = Page(new Runtime(Task.FromResult<IJSObjectReference>(module)));
        await Render(page);
        await page.DisposeAsync();
        Assert.Equal(new[] { "observeNavigation" }, module.Calls);
        Assert.Equal(1, module.Disposals);
    }

    [Fact]
    public async Task FailedImport_DoesNotPreventPageRendering()
    {
        var page = Page(new Runtime(Task.FromException<IJSObjectReference>(new JSException("Synthetic import failure"))));
        await Render(page);
        await page.DisposeAsync();
    }

    [Fact]
    public async Task DisconnectedBrowser_StillReleasesTheInteropModule()
    {
        var module = new Module { FailDisconnect = true };
        var page = Page(new Runtime(Task.FromResult<IJSObjectReference>(module)));
        await Render(page);
        await page.DisposeAsync();
        Assert.Equal(1, module.Disposals);
    }
}
