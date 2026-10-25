using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Plaid;
using Microsoft.Extensions.Options;
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
foreach (var mode in new[] { "new", "existing" })
{
    var f = await HostedRecoveryFixture.CreateAsync(mode);
    var suffix = f.SessionId.ToString("N");
    var function = "synthetic_recovery_fail_" + suffix;
    var trigger = "synthetic_recovery_guard_" + suffix;
    await using (var setup = f.OpenDirect())
    {
        var sql = $"""
            CREATE FUNCTION {function}() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN
              IF NEW."Id" = '{f.SessionId:D}'::uuid AND NEW."Status" = 'Completed' THEN
                RAISE EXCEPTION 'Synthetic final completion rollback' USING ERRCODE = 'P0001';
              END IF;
              RETURN NEW;
            END;
            $body$;
            CREATE TRIGGER {trigger} BEFORE UPDATE ON "PlaidLinkSessions"
            FOR EACH ROW EXECUTE FUNCTION {function}();
            """;
        await setup.Database.ExecuteSqlRawAsync(sql);
    }
    try
    {
        using var provider = new RecoveryProvider(f.ItemId);
        using var http = new HttpClient(provider);
        await using var db = f.Open();
        Exception? error = null;
        try { await f.Service(db, http).CheckAndCompleteAsync(f.Owner, f.SessionId); }
        catch (DbUpdateException e) { error = e; }
        Check(error is not null, "The real PostgreSQL trigger must reject final completion.");
        await using var proof = f.OpenDirect();
        var session = await proof.PlaidLinkSessions.AsNoTracking().SingleAsync(s => s.Id == f.SessionId);
        var permit = await proof.PlaidLinkCompletionAttempts.AsNoTracking().SingleAsync(a => a.SessionId == f.SessionId);
        var bank = await proof.BankConnections.AsNoTracking().SingleAsync(b => b.Id == f.BankId);
        Check(session.Status == PlaidLinkSessionStatus.Pending && session.ProtectedLinkToken == string.Empty &&
            permit.Stage == PlaidLinkCompletionStage.Interrupted,
            "A rejected transaction must preserve the durable consumed-token fence.");
        Check(bank.Status == BankConnectionStatus.RequiresAttention &&
            f.Protector.Unprotect(bank.ProtectedPlaidAccessToken!) == "owned-original",
            "Bank status and credentials must roll back with the failed final save.");
        Check(await proof.BankConnections.CountAsync(b => b.UserId == f.Owner) == 1,
            "A failed final save cannot leave a partially inserted bank.");
        var calls = provider.Polls + provider.Exchanges + provider.Items;
        var retry = await f.Service(db, http).CheckAndCompleteAsync(f.Owner, f.SessionId);
        Check(retry.Status == "RecoveryRequired" && retry.Connection is null &&
            calls == provider.Polls + provider.Exchanges + provider.Items && provider.Exchanges == 1,
            "The failed database transaction cannot authorize replay of the consumed token.");
        await f.VerifyHistoryAsync();
        Console.WriteLine($"PASS PostgreSQL server-side final rollback: {mode}");
        cases++;
    }
    finally
    {
        await using var cleanup = f.OpenDirect();
        await cleanup.Database.ExecuteSqlRawAsync($"DROP TRIGGER {trigger} ON \"PlaidLinkSessions\"; DROP FUNCTION {function}();");
    }
}

// An owner-bound FK must reject a forged permit even when both owners exist.
{
    var f = await HostedRecoveryFixture.CreateAsync("existing");
    await using (var bad = f.OpenDirect())
    {
        bad.PlaidLinkCompletionAttempts.Add(new PlaidLinkCompletionAttemptEntity
        {
            SessionId = f.SessionId, UserId = f.ForeignOwner,
            AttemptId = Guid.NewGuid(), Stage = PlaidLinkCompletionStage.Polling,
            StartedAtUtc = f.Clock.Now, UpdatedAtUtc = f.Clock.Now
        });
        Exception? error = null;
        try { await bad.SaveChangesAsync(); }
        catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException
            { SqlState: "23503", ConstraintName: "FK_PlaidCompletionAttempts_LinkSession_Owner" }) { error = e; }
        Check(error is not null, "The relational owner FK must reject cross-user session metadata.");
    }
    await using (var setup = f.OpenDirect())
    {
        setup.PlaidLinkCompletionAttempts.Add(new PlaidLinkCompletionAttemptEntity
        {
            SessionId = f.SessionId, UserId = f.Owner, Stage = PlaidLinkCompletionStage.Interrupted,
            StartedAtUtc = f.Clock.Now, UpdatedAtUtc = f.Clock.Now
        });
        await setup.SaveChangesAsync();
    }
    // Delete through a NEW context without loading the permit: exercise server cascade.
    await using (var deletion = f.OpenDirect())
    {
        deletion.PlaidLinkSessions.Remove(await deletion.PlaidLinkSessions.SingleAsync(s => s.Id == f.SessionId));
        await deletion.SaveChangesAsync();
    }
    await using var proof = f.OpenDirect();
    Check(!await proof.PlaidLinkCompletionAttempts.AnyAsync(a => a.SessionId == f.SessionId),
        "Deleting a fixture session must remove only its dependent permit.");
    await f.VerifyHistoryAsync();
    Console.WriteLine("PASS PostgreSQL owner FK rejection and unloaded-session cascade");
    cases++;
}

{
    var f = await HostedRecoveryFixture.CreateAsync("existing");
    var foreignSession = Guid.NewGuid();
    await using (var setup = f.OpenDirect())
    {
        var foreign = f.NewSession(foreignSession);
        foreign.UserId = f.ForeignOwner;
        setup.PlaidLinkSessions.Add(foreign);
        foreach (var pair in new[] { (f.SessionId, f.Owner), (foreignSession, f.ForeignOwner) })
            setup.PlaidLinkCompletionAttempts.Add(new PlaidLinkCompletionAttemptEntity
            {
                SessionId = pair.Item1, UserId = pair.Item2, Stage = PlaidLinkCompletionStage.Interrupted,
                StartedAtUtc = f.Clock.Now, UpdatedAtUtc = f.Clock.Now
            });
        await setup.SaveChangesAsync();
    }
    using var provider = new RecoveryProvider(f.ItemId);
    using var http = new HttpClient(provider);
    var api = new PlaidApiClient(http, Options.Create(new PlaidOptions {
        ClientId = "synthetic", Secret = "synthetic", Environment = PlaidOptions.SandboxEnvironment
    }));
    await using (var deletion = f.OpenDirect())
    {
        var gateway = new AccountBankDeletionGateway(deletion,
            new PlaidConnectionDisconnectService(deletion, api, f.Protector));
        await gateway.ApplyOwnedDataDeletionAsync(f.Owner);
    }
    await using var proof = f.OpenDirect();
    Check(!await proof.PlaidLinkCompletionAttempts.AnyAsync(a => a.UserId == f.Owner) &&
        !await proof.PlaidLinkSessions.AnyAsync(s => s.UserId == f.Owner),
        "Owned deletion must include the new permit metadata.");
    Check(await proof.PlaidLinkCompletionAttempts.AnyAsync(a =>
        a.UserId == f.ForeignOwner && a.SessionId == foreignSession) &&
        await proof.PlaidLinkSessions.AnyAsync(s => s.Id == foreignSession && s.UserId == f.ForeignOwner),
        "Owned deletion must preserve foreign session/permit metadata.");
    var foreignBank = await proof.BankConnections.SingleAsync(b => b.Id == f.ForeignBankId);
    var history = await proof.BankTransactions.SingleAsync(t => t.UserId == f.ForeignOwner);
    Check(f.Protector.Unprotect(foreignBank.ProtectedPlaidAccessToken!) == "foreign-original" &&
        foreignBank.TransactionsCursor == "foreign-cursor" && foreignBank.LastSuccessfulSyncAtUtc == f.HistoryTime &&
        history.Amount == 19.25m && history.IsoCurrencyCode == "EUR" && !history.IsRemoved,
        "Foreign bank credentials, cursor and monetary history must survive owned deletion.");
    Check(provider.Polls + provider.Exchanges + provider.Items == 0, "Local deletion must not contact the provider.");
    Console.WriteLine("PASS PostgreSQL owned deletion preserves foreign bank/history/permit");
    cases++;
}
Console.WriteLine($"Hosted recovery relational checks passed: {cases}; provider calls are synthetic.");

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
