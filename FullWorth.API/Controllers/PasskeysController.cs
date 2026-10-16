using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using FullWorth.API.Data.Entities;
using FullWorth.API.Data;
using FullWorth.API.Authorization;
using Microsoft.EntityFrameworkCore;
using FullWorth.API.Services.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace FullWorth.API.Controllers;

[ApiController]
[SubscriptionAccessExempt]
[Route("api")]
[EnableRateLimiting("authentication")]
public sealed class PasskeysController(
    FullWorthDbContext database,
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    IPasskeyHandler<ApplicationUser> handler,
    PasskeyCeremonyStore ceremonies,
    IOptions<FullWorthPasskeyOptions> configuration) : ControllerBase
{
    private bool Enabled => configuration.Value.Enabled;
    private IActionResult Denied() => Unauthorized(new { title = "Passkey verification failed. Start again." });

    [AllowAnonymous]
    [HttpPost("auth/passkeys/options")]
    public async Task<IActionResult> LoginOptions()
    {
        if (!Enabled) return NotFound();
        Response.Headers.CacheControl = "no-store";
        var options = await handler.MakeRequestOptionsAsync(null, HttpContext);
        var id = ceremonies.Create("login", options.AssertionState, null, null);
        return id is null ? StatusCode(503) : Ok(new { ceremonyId = id,
            options = JsonSerializer.Deserialize<JsonElement>(options.RequestOptionsJson) });
    }

    [AllowAnonymous]
    [HttpPost("auth/passkeys/login")]
    public async Task<IActionResult> Login(PasskeyCompletionRequest request)
    {
        if (!Enabled) return NotFound();
        Response.Headers.CacheControl = "no-store";
        var state = ceremonies.Consume(request.CeremonyId, "login", null);
        if (state is null) return Denied();
        var result = await handler.PerformAssertionAsync(new()
        {
            HttpContext = HttpContext, CredentialJson = request.CredentialJson,
            AssertionState = state.State
        });
        if (!result.Succeeded) return Denied();
        var user = result.User;
        if (!user.IsActive || !await signIn.CanSignInAsync(user) ||
            await users.IsLockedOutAsync(user)) return Denied();

        // A passkey does not silently replace an already-enrolled authenticator.
        if (await users.GetTwoFactorEnabledAsync(user) &&
            (string.IsNullOrWhiteSpace(request.TwoFactorCode) ||
             !await users.VerifyTwoFactorTokenAsync(user,
                users.Options.Tokens.AuthenticatorTokenProvider, Normalize(request.TwoFactorCode))))
            return Denied();

        var saved = await users.AddOrUpdatePasskeyAsync(user, result.Passkey);
        if (!saved.Succeeded) return Denied();
        user.LastLoginAtUtc = DateTimeOffset.UtcNow;
        if (!(await users.UpdateAsync(user)).Succeeded) return Denied();
        var principal = await signIn.CreateUserPrincipalAsync(user);
        return SignIn(principal, IdentityConstants.BearerScheme);
    }

    [Authorize]
    [HttpGet("account/security/passkeys")]
    public async Task<IActionResult> List()
    {
        if (!Enabled) return NotFound();
        Response.Headers.CacheControl = "no-store";
        var user = await CurrentUser();
        if (user is null) return NotFound();
        var keys = await users.GetPasskeysAsync(user);
        return Ok(keys.Select(key => new
        {
            id = WebEncoders.Base64UrlEncode(key.CredentialId),
            name = key.Name ?? "Security key", createdAt = key.CreatedAt
        }));
    }

    [Authorize]
    [HttpPost("account/security/passkeys/options")]
    public async Task<IActionResult> RegisterOptions(PasskeyEnrollmentRequest request)
    {
        if (!Enabled) return NotFound();
        Response.Headers.CacheControl = "no-store";
        var user = await CurrentUser();
        if (user is null) return NotFound();
        if (!await CheckCredentials(user, request.CurrentPassword, request.TwoFactorCode)) return Denied();
        if ((await users.GetPasskeysAsync(user)).Count >= 10) return BadRequest();
        var options = await handler.MakeCreationOptionsAsync(new()
        {
            Id = user.Id.ToString(), Name = user.Email ?? user.Id.ToString(),
            DisplayName = user.Email ?? "FullWorth account"
        }, HttpContext);
        var id = ceremonies.Create("register", options.AttestationState, user.Id,
            await users.GetSecurityStampAsync(user));
        return id is null ? StatusCode(503) : Ok(new { ceremonyId = id,
            options = JsonSerializer.Deserialize<JsonElement>(options.CreationOptionsJson) });
    }

    [Authorize]
    [HttpPost("account/security/passkeys/register")]
    public async Task<IActionResult> Register(PasskeyCompletionRequest request)
    {
        if (!Enabled) return NotFound();
        var user = await CurrentUser();
        if (user is null) return NotFound();
        var state = ceremonies.Consume(request.CeremonyId, "register", user.Id);
        if (state is null || state.SecurityStamp != await users.GetSecurityStampAsync(user)) return Denied();
        var result = await handler.PerformAttestationAsync(new()
        {
            HttpContext = HttpContext, CredentialJson = request.CredentialJson,
            AttestationState = state.State
        });
        if (!result.Succeeded || result.UserEntity.Id != user.Id.ToString()) return Denied();
        // Serialize registration writes in PostgreSQL. Identity's upsert could otherwise
        // replace a concurrently registered credential after an earlier ownership check.
        // This also makes the per-account quota atomic across API processes.
        await using var transaction = database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(HttpContext.RequestAborted) : null;
        if (transaction is not null)
            await database.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(74128596321048)", HttpContext.RequestAborted);
        // Identity's upsert must never replace another credential, even for this owner.
        if (await users.FindByPasskeyIdAsync(result.Passkey.CredentialId) is not null) return Conflict();
        if ((await users.GetPasskeysAsync(user)).Count >= 10) return BadRequest();
        result.Passkey.Name = string.IsNullOrWhiteSpace(request.Name) ? "Passkey or security key" : request.Name.Trim();
        if (!(await users.AddOrUpdatePasskeyAsync(user, result.Passkey)).Succeeded) return Denied();
        if (transaction is not null) await transaction.CommitAsync(HttpContext.RequestAborted);
        return NoContent();
    }

    [Authorize]
    [HttpPost("account/security/passkeys/remove")]
    public async Task<IActionResult> Remove(PasskeyRemovalRequest request)
    {
        if (!Enabled) return NotFound();
        var user = await CurrentUser();
        if (user is null) return NotFound();
        if (!await CheckCredentials(user, request.CurrentPassword, request.TwoFactorCode)) return Denied();
        byte[] id;
        try { id = WebEncoders.Base64UrlDecode(request.CredentialId); }
        catch (FormatException) { return NotFound(); }
        if (!(await users.GetPasskeysAsync(user)).Any(key => key.CredentialId.AsSpan().SequenceEqual(id))) return NotFound();
        return (await users.RemovePasskeyAsync(user, id)).Succeeded ? NoContent() : Denied();
    }

    private async Task<ApplicationUser?> CurrentUser()
    {
        var user = await users.GetUserAsync(User);
        return user?.IsActive == true ? user : null;
    }

    private async Task<bool> CheckCredentials(ApplicationUser user, string password, string? code)
    {
        if (string.IsNullOrWhiteSpace(password) ||
            !(await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true)).Succeeded)
            return false;
        return !await users.GetTwoFactorEnabledAsync(user) ||
            (!string.IsNullOrWhiteSpace(code) && await users.VerifyTwoFactorTokenAsync(user,
                users.Options.Tokens.AuthenticatorTokenProvider, Normalize(code)));
    }

    private static string Normalize(string code) => code.Replace(" ", "").Replace("-", "");
}

public sealed record PasskeyEnrollmentRequest(
    [Required, MaxLength(1024)] string CurrentPassword,
    [MaxLength(12)] string? TwoFactorCode);
public sealed record PasskeyCompletionRequest(
    [Required, StringLength(43, MinimumLength = 43)] string CeremonyId,
    [Required, MaxLength(32768)] string CredentialJson,
    [MaxLength(80)] string? Name,
    [MaxLength(12)] string? TwoFactorCode);
public sealed record PasskeyRemovalRequest(
    [Required, MaxLength(1400)] string CredentialId,
    [Required, MaxLength(1024)] string CurrentPassword,
    [MaxLength(12)] string? TwoFactorCode);
