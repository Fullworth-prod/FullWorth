namespace FullWorth.Services;

// Only device storage is replaced. HTTP/auth/view-model are the shipped sources.
// This test double does not establish iOS Keychain correctness.
public sealed class AuthSession
{
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    private DateTimeOffset? _expiresAt;
    public Task SaveAsync(string accessToken, string refreshToken, long expiresInSeconds)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        _expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);
        return Task.CompletedTask;
    }
    public Task<string?> GetAccessTokenAsync() => Task.FromResult(AccessToken);
    public Task<string?> GetRefreshTokenAsync() => Task.FromResult(RefreshToken);
    public Task<DateTimeOffset?> GetAccessTokenExpiresAtUtcAsync() => Task.FromResult(_expiresAt);
    public void Clear() { AccessToken = null; RefreshToken = null; _expiresAt = null; }
}
