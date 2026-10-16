using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace FullWorth.API.Services.Identity;

// Intentionally single-process: a restart or a different replica fails closed.
// Never serialize this state to the client: attestation state contains the owner.
public sealed class PasskeyCeremonyStore(TimeProvider clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(3);
    private const int Capacity = 2048;
    private readonly object _gate = new();
    private readonly Dictionary<string, Ceremony> _pending = new(StringComparer.Ordinal);

    public string? Create(string operation, string? state, Guid? userId, string? securityStamp)
    {
        lock (_gate)
        {
            var now = clock.GetUtcNow();
            foreach (var expiredId in _pending.Where(x => x.Value.ExpiresAt <= now).Select(x => x.Key).ToArray())
                _pending.Remove(expiredId);
            if (_pending.Count >= Capacity) return null;
            var id = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            _pending.Add(id, new(operation, state, userId, securityStamp, now + Lifetime));
            return id;
        }
    }

    public Ceremony? Consume(string? id, string operation, Guid? userId)
    {
        if (id is null || id.Length != 43) return null;
        lock (_gate)
        {
            if (!_pending.Remove(id, out var item)) return null;
            return item.ExpiresAt > clock.GetUtcNow() &&
                item.Operation == operation && item.UserId == userId ? item : null;
        }
    }

    public sealed record Ceremony(string Operation, string? State, Guid? UserId,
        string? SecurityStamp, DateTimeOffset ExpiresAt);
}
