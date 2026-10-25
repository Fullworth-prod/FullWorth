using FullWorth.API.Data.Entities;
using FullWorth.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

if (Environment.GetEnvironmentVariable("FULLWORTH_LINK_RECOVERY_POSTGRES") != "ci-fixture-only")
    throw new InvalidOperationException("Explicit disposable PostgreSQL fixture required.");

var cases = 0;
foreach (var mode in new[] { "new", "existing" })
foreach (var phase in new[] { "claim", "fence", "completion" })
{
    await using var proxy = new LoopbackCommitProxy();
    var f = await HostedRecoveryFixture.CreateAsync(mode, proxyPort: proxy.Port);
    using var provider = new RecoveryProvider(f.ItemId);
    using var http = new HttpClient(provider);
    if (phase == "claim") proxy.Arm("INSERT 0 1");
    provider.BeforeResponse = (path, _) =>
    {
        if ((phase == "fence" && path == "/link/token/get") ||
            (phase == "completion" && path == "/item/get"))
            proxy.Arm("COMMIT");
        return Task.CompletedTask;
    };

    await using var db = f.Open();
    var service = f.Service(db, http);
    Exception? error = null;
    string? firstStatus = null;
    try { firstStatus = (await service.CheckAndCompleteAsync(f.Owner, f.SessionId)).Status; }
    catch (Exception e) { error = e; }
    await proxy.Dropped.WaitAsync(TimeSpan.FromSeconds(20));
    Check(proxy.Drops == 1, "Exactly one database acknowledgement must be lost.");
    if (phase == "completion")
        Check(error is not null, "A lost final commit reply must surface as uncertain.");
    else
        Check(error is null && firstStatus == "Completed",
            "A matching durable metadata nonce must reconcile without another provider operation.");

    await using (var proof = f.OpenDirect())
    {
        var session = await proof.PlaidLinkSessions.AsNoTracking().SingleAsync(s => s.Id == f.SessionId);
        var permit = await proof.PlaidLinkCompletionAttempts.AsNoTracking()
            .SingleAsync(a => a.SessionId == f.SessionId && a.UserId == f.Owner);
        Check(session.Status == PlaidLinkSessionStatus.Completed &&
            permit.Stage == PlaidLinkCompletionStage.Completed && session.ProtectedLinkToken == string.Empty,
            "The real server must have committed the owner result and completed permit.");
        Check(await proof.BankConnections.CountAsync(b => b.UserId == f.Owner && b.PlaidItemId == f.ItemId) == 1,
            "Exactly one owned bank may represent the provider item.");
    }
    var calls = provider.Polls + provider.Exchanges + provider.Items;
    var replay = await service.CheckAndCompleteAsync(f.Owner, f.SessionId);
    Check(replay.Status == "Completed" && replay.Connection is not null, "Replay must return committed truth.");
    Check(calls == provider.Polls + provider.Exchanges + provider.Items &&
        provider.Exchanges == 1 && provider.MaximumExchangesPerToken == 1,
        "A lost acknowledgement cannot authorize a second exchange.");
    await f.VerifyHistoryAsync();
    Console.WriteLine($"PASS PostgreSQL network acknowledgement loss: {mode}/{phase}");
    cases++;
}
Console.WriteLine($"Hosted recovery network checks passed: {cases}; provider calls are synthetic.");

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
