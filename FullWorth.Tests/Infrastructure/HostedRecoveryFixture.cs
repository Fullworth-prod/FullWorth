using System.Net;
using System.Text;
using System.Text.Json;
using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Plaid;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Npgsql;

namespace FullWorth.Tests.Infrastructure;

internal sealed class HostedRecoveryFixture
{
    internal Guid Owner { get; } = Guid.NewGuid();
    internal Guid ForeignOwner { get; } = Guid.NewGuid();
    internal Guid SessionId { get; } = Guid.NewGuid();
    internal Guid BankId { get; } = Guid.NewGuid();
    internal Guid ForeignBankId { get; } = Guid.NewGuid();
    internal Guid AccountId { get; } = Guid.NewGuid();
    internal Guid ForeignAccountId { get; } = Guid.NewGuid();
    internal string ItemId { get; } = "synthetic-item-" + Guid.NewGuid().ToString("N");
    internal PlaidTokenProtector Protector { get; } = new(new EphemeralDataProtectionProvider());
    internal RecoveryClock Clock { get; } = new();
    internal DbContextOptions<FullWorthDbContext> Options { get; private set; } = null!;
    internal DbContextOptions<FullWorthDbContext> DirectOptions { get; private set; } = null!;
    internal bool Relational { get; private set; }
    internal DateTimeOffset HistoryTime { get; private set; }

    internal FullWorthDbContext Open() => new(Options);
    internal FullWorthDbContext OpenDirect() => new(DirectOptions);

    internal static async Task<HostedRecoveryFixture> CreateAsync(string mode,
        SaveChangesInterceptor? fault = null, int? proxyPort = null)
    {
        var f = new HostedRecoveryFixture();
        var builder = new DbContextOptionsBuilder<FullWorthDbContext>();
        var enabled = Environment.GetEnvironmentVariable("FULLWORTH_LINK_RECOVERY_POSTGRES");
        NpgsqlConnectionStringBuilder? connection = null;
        if (enabled is null)
        {
            if (proxyPort is not null) throw new InvalidOperationException("Network tests require the explicit fixture.");
            builder.UseInMemoryDatabase(Guid.NewGuid().ToString("N"));
        }
        else
        {
            if (enabled != "ci-fixture-only")
                throw new InvalidOperationException("Explicit disposable-fixture opt-in required.");
            connection = new(Environment.GetEnvironmentVariable("FULLWORTH_LINK_RECOVERY_DATABASE")
                ?? throw new InvalidOperationException("Disposable fixture connection missing."));
            if (connection.Host != "127.0.0.1" || connection.Port != 5432 ||
                connection.Database != "fullworth_link_recovery_fixture" || connection.Username != "fixture")
                throw new InvalidOperationException("Only the fixed loopback disposable database is allowed.");
            connection.SslMode = SslMode.Disable;
            builder.UseNpgsql(connection.ConnectionString);
            f.Relational = true;
        }
        if (fault is not null) builder.AddInterceptors(fault);
        f.DirectOptions = builder.Options;
        f.Options = builder.Options;
        f.HistoryTime = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        await using (var db = f.OpenDirect())
        {
            if (f.Relational) await db.Database.MigrateAsync();
            db.Users.AddRange(new ApplicationUser { Id = f.Owner }, new ApplicationUser { Id = f.ForeignOwner });
            db.BankConnections.AddRange(new BankConnectionEntity
            {
                Id = f.BankId, UserId = f.Owner, InstitutionName = "Owned synthetic bank",
                PlaidItemId = mode == "existing" ? f.ItemId : "other-" + f.ItemId,
                Status = BankConnectionStatus.RequiresAttention,
                ProtectedPlaidAccessToken = f.Protector.Protect("owned-original"),
                TransactionsCursor = "owned-cursor", LastSuccessfulSyncAtUtc = f.HistoryTime,
                CreatedAtUtc = f.HistoryTime, UpdatedAtUtc = f.HistoryTime
            }, new BankConnectionEntity
            {
                Id = f.ForeignBankId, UserId = f.ForeignOwner, InstitutionName = "Foreign synthetic bank",
                PlaidItemId = "foreign-" + f.ItemId, Status = BankConnectionStatus.Active,
                ProtectedPlaidAccessToken = f.Protector.Protect("foreign-original"),
                TransactionsCursor = "foreign-cursor", LastSuccessfulSyncAtUtc = f.HistoryTime,
                CreatedAtUtc = f.HistoryTime, UpdatedAtUtc = f.HistoryTime
            });
            db.BankAccounts.AddRange(new BankAccountEntity
            {
                Id = f.AccountId, UserId = f.Owner, BankConnectionId = f.BankId,
                PlaidAccountId = "owned-" + f.ItemId, Name = "Owned synthetic account"
            }, new BankAccountEntity
            {
                Id = f.ForeignAccountId, UserId = f.ForeignOwner, BankConnectionId = f.ForeignBankId,
                PlaidAccountId = "foreign-" + f.ItemId, Name = "Foreign synthetic account"
            });
            foreach (var pair in new[] { (f.Owner, f.AccountId), (f.ForeignOwner, f.ForeignAccountId) })
                db.BankTransactions.Add(new BankTransactionEntity
                {
                    UserId = pair.Item1, BankAccountId = pair.Item2,
                    PlaidTransactionId = Guid.NewGuid().ToString("N"), Name = "Synthetic history",
                    Amount = 19.25m, IsoCurrencyCode = "EUR", PostedDate = new DateOnly(2026, 10, 1),
                    CreatedAtUtc = f.HistoryTime, UpdatedAtUtc = f.HistoryTime
                });
            db.PlaidLinkSessions.Add(f.NewSession(f.SessionId));
            await db.SaveChangesAsync();
        }
        if (proxyPort is not null)
        {
            connection!.Port = proxyPort.Value;
            connection.Pooling = false;
            var proxied = new DbContextOptionsBuilder<FullWorthDbContext>()
                .UseNpgsql(connection.ConnectionString);
            if (fault is not null) proxied.AddInterceptors(fault);
            f.Options = proxied.Options;
        }
        return f;
    }

    internal PlaidLinkSessionEntity NewSession(Guid id) => new()
    {
        Id = id, UserId = Owner, ExpiresAtUtc = Clock.Now.AddHours(2),
        ProtectedLinkToken = Protector.ProtectLinkToken("synthetic-link-" + id.ToString("N")),
        CreatedAtUtc = Clock.Now, UpdatedAtUtc = Clock.Now
    };

    internal PlaidHostedLinkCompletionService Service(FullWorthDbContext db, HttpClient http)
    {
        var api = new PlaidApiClient(http, Microsoft.Extensions.Options.Options.Create(new PlaidOptions
        {
            ClientId = "synthetic-client", Secret = "synthetic-secret",
            Environment = PlaidOptions.SandboxEnvironment
        }));
        return new(db, api, Protector, new PlaidConnectionExchangeService(api, Protector, db, Clock), Clock);
    }

    internal async Task VerifyHistoryAsync()
    {
        await using var db = OpenDirect();
        var bank = await db.BankConnections.AsNoTracking().SingleAsync(x => x.Id == BankId);
        var foreign = await db.BankConnections.AsNoTracking().SingleAsync(x => x.Id == ForeignBankId);
        var rows = await db.BankTransactions.AsNoTracking()
            .Where(x => x.UserId == Owner || x.UserId == ForeignOwner).ToListAsync();
        if (bank.TransactionsCursor != "owned-cursor" || foreign.TransactionsCursor != "foreign-cursor" ||
            bank.LastSuccessfulSyncAtUtc != HistoryTime || foreign.LastSuccessfulSyncAtUtc != HistoryTime ||
            Protector.Unprotect(foreign.ProtectedPlaidAccessToken!) != "foreign-original" ||
            rows.Count != 2 || rows.Any(x => x.Amount != 19.25m || x.IsoCurrencyCode != "EUR" ||
                x.IsRemoved || x.UpdatedAtUtc != HistoryTime))
            throw new InvalidOperationException("Recovery changed unrelated owned/foreign bank history.");
    }
}

internal sealed class RecoveryClock : TimeProvider
{
    internal DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class RecoveryProvider(string itemId) : HttpMessageHandler
{
    internal int Polls, Exchanges, Items;
    internal Func<string, CancellationToken, Task>? BeforeResponse { get; set; }
    internal bool Pending { get; set; }
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _tokens = new();
    internal int MaximumExchangesPerToken => _tokens.Values.DefaultIfEmpty(0).Max();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var endpoint = request.RequestUri!.AbsolutePath;
        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        if (endpoint == "/link/token/get") Interlocked.Increment(ref Polls);
        else if (endpoint == "/item/public_token/exchange")
        {
            Interlocked.Increment(ref Exchanges);
            var token = body.RootElement.GetProperty("public_token").GetString()!;
            if (_tokens.AddOrUpdate(token, 1, (_, count) => count + 1) != 1)
                throw new InvalidOperationException("Synthetic provider rejected a reused one-time token.");
        }
        else if (endpoint == "/item/get") Interlocked.Increment(ref Items);
        else throw new InvalidOperationException("Unexpected synthetic provider endpoint.");
        if (BeforeResponse is not null) await BeforeResponse(endpoint, cancellationToken);
        object payload = endpoint switch
        {
            "/link/token/get" when Pending => new { link_sessions = Array.Empty<object>() },
            "/link/token/get" => new { link_sessions = new[] { new {
                finished_at = "2026-10-24T00:00:00Z",
                results = new { item_add_results = new[] { new {
                    public_token = "synthetic-public-" + body.RootElement.GetProperty("link_token").GetString()
                }}}
            }}},
            "/item/public_token/exchange" => new { access_token = "synthetic-new-access", item_id = itemId },
            _ => new { item = new { item_id = itemId, institution_name = "Synthetic recovered bank" } }
        };
        return new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
    }
}
