using System.Net;
using System.Text.Json;
using FullWorth.Tests.Infrastructure;

namespace FullWorth.Tests.Web;

public sealed class WhatsNewReleaseNotesTests
{
    [Fact]
    public async Task PublishedNotes_AreAvailableAndDescribeRealUserFacingChanges()
    {
        using var factory = new FullWorthWebFactory();
        using var client = factory.CreateHttpsClient();
        using var response = await client.GetAsync("/release-notes.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var notes = document.RootElement;
        var id = notes.GetProperty("id").GetString();
        Assert.NotNull(id);
        Assert.Matches("^[a-z0-9][a-z0-9._-]{3,79}$", id);
        Assert.NotEmpty(notes.GetProperty("title").GetString()!);
        Assert.NotEmpty(notes.GetProperty("summary").GetString()!);
        Assert.NotEmpty(notes.GetProperty("version").GetString()!);

        var highlights = notes.GetProperty("highlights").EnumerateArray().ToArray();
        Assert.InRange(highlights.Length, 1, 6);
        foreach (var highlight in highlights)
        {
            Assert.Contains(highlight.GetProperty("icon").GetString(),
                new[] { "scan", "trend-up", "shield", "check" });
            Assert.NotEmpty(highlight.GetProperty("title").GetString()!);
            Assert.True(highlight.GetProperty("description").GetString()!.Length >= 35);
        }

        var copy = string.Join(" ", highlights.Select(x =>
            x.GetProperty("description").GetString()));
        Assert.Contains("Missing a bill?", copy, StringComparison.Ordinal);
        Assert.Contains("currency", copy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("review", copy, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("A cleaner, safer release", copy, StringComparison.Ordinal);
    }

    [Fact]
    public void Dialog_UsesCuratedReleaseNotesInsteadOfGenericShaHighlights()
    {
        var root = FindRepositoryRoot();
        var dialog = File.ReadAllText(Path.Combine(root,
            "FullWorth.Web", "Components", "Layout", "WhatsNewDialog.razor"));
        var script = File.ReadAllText(Path.Combine(root,
            "FullWorth.Web", "wwwroot", "js", "whats-new.js"));

        Assert.Contains("OnNotesAvailable", dialog, StringComparison.Ordinal);
        Assert.Contains("_notes.Highlights", dialog, StringComparison.Ordinal);
        Assert.Contains("acknowledge", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("Sharper bill intelligence", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("BILLWATCH_RELEASE_ID", dialog, StringComparison.Ordinal);

        Assert.Contains("cache: \"no-store\"", script, StringComparison.Ordinal);
        Assert.Contains("visibilitychange", script, StringComparison.Ordinal);
        Assert.Contains("pageshow", script, StringComparison.Ordinal);
        Assert.Contains("focus", script, StringComparison.Ordinal);
        Assert.Contains("setInterval", script, StringComparison.Ordinal);
        Assert.Contains("localStorage.setItem", script, StringComparison.Ordinal);
        Assert.Contains("localStorage.getItem", script, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "FullWorth.slnx")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("FullWorth solution root not found.");
    }
}
