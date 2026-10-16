using Microsoft.AspNetCore.Identity;

namespace FullWorth.API.Services.Identity;

public sealed class FullWorthPasskeyOptions
{
    public bool Enabled { get; set; }
    public string Origin { get; set; } = "https://fullworth.org";

    public static bool IsValidOrigin(string? origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.Query) &&
        string.IsNullOrEmpty(uri.Fragment) && string.IsNullOrEmpty(uri.UserInfo) &&
        origin == uri.GetLeftPart(UriPartial.Authority);

    public bool AcceptOrigin(PasskeyOriginValidationContext context) =>
        !context.CrossOrigin && context.TopOrigin is null &&
        string.Equals(context.Origin, Origin, StringComparison.Ordinal);
}
