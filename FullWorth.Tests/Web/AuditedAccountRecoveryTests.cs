using System.Reflection;
using FullWorth.Web.Components.Pages.App;
using Microsoft.JSInterop;

namespace FullWorth.Tests.Web;

public sealed class AuditedAccountRecoveryTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Theory]
    [InlineData("QueueBetaProgram", "BetaTester")]
    [InlineData("QueueInternalProgram", "InternalTester")]
    public async Task ProgramRemovalCollectsAndForwardsRequiredProof(string queue, string program)
    {
        var page = new AdminUserControls { UserId = Guid.NewGuid(), Programs = new[] { program } };
        InjectLocalizer(page);
        Set(page, "_reauthCurrentPassword", "Synthetic!Proof123");
        Set(page, "_reauthTwoFactorCode", "123456");
        Call(page, queue);
        Assert.True(Get<bool>(page, "RequiresPrivilegeGrantReauthentication"));
        var pending = Get<object>(page, "_pending");
        var module = new CaptureModule();
        await (Task)typeof(AdminUserControls).GetMethod("MutateProgramAsync", Flags)!
            .Invoke(page, new[] { module, pending })!;
        Assert.Equal("setAdminProgram", module.Method);
        var request = module.Arguments![2]!;
        Assert.False((bool)request.GetType().GetProperty("IsActive")!.GetValue(request)!);
        Assert.Equal("Synthetic!Proof123", request.GetType().GetProperty("CurrentPassword")!.GetValue(request));
        Assert.Equal("123456", request.GetType().GetProperty("TwoFactorCode")!.GetValue(request));
    }

    [Theory]
    [InlineData("FullWorthPerformance.getTransactionLoadLimit")]
    [InlineData("FullWorthPerformance.getExecutionMode")]
    public async Task OptionalPerformanceFailureUsesBoundedServerFallback(string failedMethod)
    {
        var page = new AccountTransactions();
        typeof(AccountTransactions).GetProperty("JS", Flags)!.SetValue(page, new PerformanceRuntime(failedMethod));
        await (Task)Call(page, "EnsureInitialTransactionLoadLimitAsync")!;
        Assert.Equal(100, Get<int>(page, "_transactionLoadLimit"));
        Assert.False(Get<bool>(page, "_useLocalTransactionFiltering"));
        Assert.True(Get<bool>(page, "_hasResolvedInitialTransactionLoadLimit"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RefreshedAccountOptionsReconcileOnlyMissingSelection(bool retained)
    {
        var page = new AccountTransactions();
        var selected = Guid.NewGuid();
        Set(page, "_selectedAccountId", selected.ToString("D"));
        Set(page, "_searchText", "keep this search");
        Set(page, "_transactions", new List<AccountTransactions.BankTransactionViewModel>
        {
            new() { Id = Guid.NewGuid(), BankAccountId = retained ? selected : Guid.NewGuid() }
        });
        Call(page, "UpdateTransactionSummaries");
        Assert.Equal(retained ? selected.ToString("D") : "", Get<string>(page, "_selectedAccountId"));
        Assert.Equal("keep this search", Get<string>(page, "_searchText"));
    }

    private static void InjectLocalizer(AdminUserControls page)
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Microsoft.Extensions.DependencyInjection.LoggingServiceCollectionExtensions.AddLogging(services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        var provider = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);
        typeof(AdminUserControls).GetProperty("L", Flags)!.SetValue(page,
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<
                Microsoft.Extensions.Localization.IStringLocalizer<AdminUserControls>>(provider));
    }
    private static object? Call(object page, string name) => page.GetType().GetMethod(name, Flags)!.Invoke(page, null);
    private static void Set(object page, string name, object value) => page.GetType().GetField(name, Flags)!.SetValue(page, value);
    private static T Get<T>(object page, string name) => (T)(page.GetType().GetField(name, Flags)?.GetValue(page)
        ?? page.GetType().GetProperty(name, Flags)!.GetValue(page))!;

    private sealed class PerformanceRuntime(string failedMethod) : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => InvokeAsync<T>(identifier, default, args);
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken token, object?[]? args)
        {
            if (identifier == failedMethod) throw new JSException("helper unavailable");
            return ValueTask.FromResult((T)(object)500);
        }
    }
    private sealed class CaptureModule : IJSObjectReference
    {
        public string? Method;
        public object?[]? Arguments;
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => InvokeAsync<T>(identifier, default, args);
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken token, object?[]? args)
        {
            Method = identifier; Arguments = args;
            return ValueTask.FromResult(default(T)!);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
