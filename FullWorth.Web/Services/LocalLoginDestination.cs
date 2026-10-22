namespace FullWorth.Web.Services;

// Login destinations are navigation hints, never authorization decisions.
public static class LocalLoginDestination
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2048) return "/app";
        var decoded = value;
        try
        {
            for (var round = 0; round < 4; round++)
            {
                if (!Safe(decoded)) return "/app";
                var next = Uri.UnescapeDataString(decoded);
                if (next == decoded) return value;
                decoded = next;
            }
        }
        catch (UriFormatException) { }
        return "/app";
    }

    private static bool Safe(string value) =>
        value.StartsWith("/", StringComparison.Ordinal) &&
        !value.StartsWith("//", StringComparison.Ordinal) &&
        !value.Contains('\\') && !value.Any(char.IsControl);
}
