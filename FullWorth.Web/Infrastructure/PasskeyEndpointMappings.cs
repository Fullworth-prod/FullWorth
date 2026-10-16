using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using FullWorth.Web.Services;
using Microsoft.AspNetCore.Antiforgery;

namespace FullWorth.Web.Infrastructure;

public static class PasskeyEndpointMappings
{
    public static IEndpointRouteBuilder MapFullWorthPasskeys(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/auth/passkeys/options", async (HttpContext context,
            IAntiforgery antiforgery, IHttpClientFactory clients, IConfiguration configuration) =>
        {
            await antiforgery.ValidateRequestAsync(context);
            context.Response.Headers.CacheControl = "no-store";
            if (!configuration.GetValue<bool>("Passkeys:Enabled")) return Results.NotFound();
            using var response = await clients.CreateClient("FullWorthApi")
                .PostAsJsonAsync("/api/auth/passkeys/options", new { }, context.RequestAborted);
            return response.IsSuccessStatusCode
                ? Results.Content(await response.Content.ReadAsStringAsync(context.RequestAborted), "application/json")
                : Results.StatusCode((int)response.StatusCode);
        }).RequireRateLimiting(AuthEndpointMappings.AuthenticationRateLimitPolicy);

        endpoints.MapPost("/auth/passkeys/login", async (HttpContext context,
            IAntiforgery antiforgery, WebAuthenticationService authentication, IConfiguration configuration,
            PasskeyLoginBffRequest request) =>
        {
            await antiforgery.ValidateRequestAsync(context);
            context.Response.Headers.CacheControl = "no-store";
            if (!configuration.GetValue<bool>("Passkeys:Enabled")) return Results.NotFound();
            if (string.IsNullOrEmpty(request.CeremonyId) || request.CeremonyId.Length != 43 ||
                string.IsNullOrEmpty(request.CredentialJson) || request.CredentialJson.Length > 32768 ||
                request.TwoFactorCode?.Length > 12) return Results.BadRequest();
            var result = await authentication.LoginPasskeyAsync(context, request,
                request.RememberMe, context.RequestAborted);
            return result.Succeeded ? Results.NoContent() : Results.Unauthorized();
        }).RequireRateLimiting(AuthEndpointMappings.AuthenticationRateLimitPolicy);
        return endpoints;
    }
}

public sealed record PasskeyLoginBffRequest(string CeremonyId, string CredentialJson,
    string? TwoFactorCode, bool RememberMe);
