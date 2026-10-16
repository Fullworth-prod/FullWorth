using FullWorth.API.Services.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace FullWorth.Tests.Security;

public sealed class PasskeyCeremonyStoreTests
{
    [Fact]
    public void ChallengeIsSingleUseAndOwnerBound()
    {
        var store = new PasskeyCeremonyStore(TimeProvider.System);
        var user = Guid.NewGuid();
        var id = store.Create("register", "server-only-state", user, "stamp");
        Assert.NotNull(id);
        Assert.Equal("server-only-state", store.Consume(id, "register", user)?.State);
        Assert.Null(store.Consume(id, "register", user));
        id = store.Create("register", "state", user, "stamp");
        Assert.Null(store.Consume(id, "register", Guid.NewGuid()));
        Assert.Null(store.Consume(id, "register", user));
    }

    [Fact]
    public void WrongOperationAndExpiredStateFailClosed()
    {
        var clock = new Clock();
        var store = new PasskeyCeremonyStore(clock);
        var id = store.Create("login", "state", null, null);
        Assert.Null(store.Consume(id, "register", null));
        id = store.Create("login", "state", null, null);
        clock.Now += PasskeyCeremonyStore.Lifetime;
        Assert.Null(store.Consume(id, "login", null));
    }

    [Fact]
    public async Task ConcurrentRequestsOnlyConsumeOnce()
    {
        var store = new PasskeyCeremonyStore(TimeProvider.System);
        var id = store.Create("login", "state", null, null);
        var results = await Task.WhenAll(Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => store.Consume(id, "login", null))));
        Assert.Single(results, x => x is not null);
    }

    [Fact]
    public void CapacityIsBoundedAndExpiredEntriesAreReclaimed()
    {
        var clock = new Clock();
        var store = new PasskeyCeremonyStore(clock);
        for (var i = 0; i < 2048; i++)
            Assert.NotNull(store.Create("login", "state", null, null));
        Assert.Null(store.Create("login", "state", null, null));
        clock.Now += PasskeyCeremonyStore.Lifetime;
        Assert.NotNull(store.Create("login", "state", null, null));
    }

    [Theory]
    [InlineData("https://fullworth.org", true)]
    [InlineData("https://fullworth.org.evil.example", true)]
    [InlineData("http://fullworth.org", false)]
    [InlineData("https://fullworth.org/", false)]
    [InlineData("https://fullworth.org/path", false)]
    [InlineData("https://user@fullworth.org", false)]
    public void CanonicalHttpsConfigurationOriginRequired(string origin, bool expected) =>
        Assert.Equal(expected, FullWorthPasskeyOptions.IsValidOrigin(origin));

    [Fact]
    public void CrossOriginFramesAreRejected()
    {
        var options = new FullWorthPasskeyOptions();
        var context = new PasskeyOriginValidationContext
        {
            HttpContext = new DefaultHttpContext(), Origin = options.Origin, CrossOrigin = false
        };
        Assert.True(options.AcceptOrigin(context));
        Assert.False(options.AcceptOrigin(new()
        {
            HttpContext = context.HttpContext, Origin = options.Origin, CrossOrigin = true
        }));
        foreach (var origin in new[] { "https://evil.example", "https://fullworth.org.evil.example" })
            Assert.False(options.AcceptOrigin(new()
            {
                HttpContext = context.HttpContext, Origin = origin, CrossOrigin = false
            }));
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
