namespace FullWorth.Core.Configuration;

public enum FullWorthWebDestination
{
    Blocked,
    InApp,
    ExternalBrowser
}

/// <summary>Top-level navigation policy, not a sandbox for website subresources.</summary>
public static class FullWorthWebNavigation
{
    public static Uri EntryPoint { get; } = new("https://fullworth.org/app");

    public static FullWorthWebDestination Classify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Any(char.IsControl) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            uri.Port != 443 ||
            uri.HostNameType != UriHostNameType.Dns)
        {
            return FullWorthWebDestination.Blocked;
        }

        return string.Equals(uri.IdnHost, EntryPoint.IdnHost, StringComparison.OrdinalIgnoreCase)
            ? FullWorthWebDestination.InApp
            : FullWorthWebDestination.ExternalBrowser;
    }
}
