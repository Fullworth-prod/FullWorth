using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace FullWorth.Tests.Web;

public sealed class SpanishResourceCoverageTests
{
    private static readonly Regex LocalizerInjection = new(
        @"@inject\s+(?:[\w.]+\.)?IStringLocalizer<(?<type>[^>]+)>\s+(?<alias>\w+)");
    private static readonly Regex FormatArgument = new(
        @"(?<!\{)\{(\d+)[^{}]*\}(?!\})");

    [Fact]
    public void EveryLiteralKeyInLocalizedRazorComponents_HasASpanishResource()
    {
        var root = RepositoryRoot();
        var sourceRoot = Path.Combine(root, "FullWorth.Web", "Components");
        var missing = new List<string>();
        var audited = new HashSet<string>(StringComparer.Ordinal);

        foreach (var sourcePath in Directory.EnumerateFiles(sourceRoot, "*.razor", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(sourcePath);
            foreach (Match binding in LocalizerInjection.Matches(source))
            {
                var type = binding.Groups["type"].Value.Trim();
                var alias = binding.Groups["alias"].Value;
                var resourcePath = ResourcePath(root, sourcePath, type);
                Assert.True(File.Exists(resourcePath), $"Missing Spanish resource: {resourcePath}");
                var resources = ReadResources(resourcePath);
                var literal = new Regex(@"\b" + Regex.Escape(alias) + @"\[\s*""((?:\\.|[^""\\])*)""");
                var relativePath = Path.GetRelativePath(root, sourcePath).Replace('\\', '/');
                audited.Add(relativePath);

                foreach (Match match in literal.Matches(source))
                {
                    var key = Regex.Unescape(match.Groups[1].Value);
                    if (!resources.ContainsKey(key))
                        missing.Add($"{relativePath}: {key}");
                }
            }
        }

        // These observed fallback surfaces must never disappear from the audit.
        foreach (var path in new[]
        {
            "FullWorth.Web/Components/Pages/Home.razor",
            "FullWorth.Web/Components/Pages/App/Overview.razor",
            "FullWorth.Web/Components/Pages/App/AccountSettings.razor",
            "FullWorth.Web/Components/Pages/App/Subscription.razor",
            "FullWorth.Web/Components/Shared/StatementUploadPanel.razor"
        })
            Assert.Contains(path, audited);

        Assert.True(missing.Count == 0,
            $"Spanish fallback keys found ({missing.Count} occurrences):" +
            Environment.NewLine + string.Join(Environment.NewLine, missing.Take(25)));
    }

    [Fact]
    public void SpanishResources_HaveUniqueNonemptyValuesAndPreserveFormatArguments()
    {
        var directory = Path.Combine(RepositoryRoot(), "FullWorth.Web", "Resources");
        var paths = Directory.GetFiles(directory, "*.es.resx", SearchOption.AllDirectories);
        Assert.NotEmpty(paths);
        foreach (var path in paths)
        {
            foreach (var (key, value) in ReadResources(path))
            {
                Assert.False(string.IsNullOrWhiteSpace(value), $"Empty Spanish value: {path}: {key}");
                Assert.Equal(Arguments(key), Arguments(value));
            }
        }
    }

    [Theory]
    [InlineData(typeof(global::FullWorth.Web.Components.App), "Menu", "Menú")]
    [InlineData(typeof(global::FullWorth.Web.Components.Pages.App.Overview), "Financial pulse", "Pulso financiero")]
    [InlineData(typeof(global::FullWorth.Web.Components.Pages.App.AccountSettings), "Security & preferences", "Seguridad y preferencias")]
    [InlineData(typeof(global::FullWorth.Web.Components.Pages.App.Account), "Accounts currently available", "Cuentas disponibles actualmente")]
    [InlineData(typeof(global::FullWorth.Web.Components.Pages.App.Planning), "Coverage unknown", "Cobertura desconocida")]
    [InlineData(typeof(global::FullWorth.Web.Components.Pages.App.Profile), "Personal profile", "Perfil personal")]
    [InlineData(typeof(global::FullWorth.Web.Components.Pages.App.Subscription), "Cancellation scheduled", "Cancelación programada")]
    [InlineData(typeof(global::FullWorth.Web.Components.Pages.App.Subscription), "Yearly", "Anual")]
    [InlineData(typeof(global::FullWorth.Web.Components.Pages.App.Subscription), "Monthly", "Mensual")]
    [InlineData(typeof(global::FullWorth.Web.Components.Shared.StatementUploadPanel), "Upload not confirmed", "Carga no confirmada")]
    public void CompiledResources_ResolveSpanishAndKeepTheEnglishFallback(
        Type component, string key, string expectedSpanish)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            using var services = LocalizationServices();
            var localizer = services.GetRequiredService<IStringLocalizerFactory>().Create(component);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("es");
            var translated = localizer[key];
            Assert.False(translated.ResourceNotFound, $"Compiled Spanish resource missing: {component}: {key}");
            Assert.Equal(expectedSpanish, translated.Value);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal(key, localizer[key].Value);
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [Fact]
    public void CompiledSubscriptionResource_FormatsTheOriginalCurrencyTextWithoutConversion()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("es");
            using var services = LocalizationServices();
            var localizer = services.GetRequiredService<IStringLocalizerFactory>()
                .Create(typeof(global::FullWorth.Web.Components.Pages.App.Subscription));
            var result = localizer["About {0} per month when billed yearly", "5,00 USD"];
            Assert.False(result.ResourceNotFound);
            Assert.Equal("Aproximadamente 5,00 USD al mes con facturación anual", result.Value);
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    private static ServiceProvider LocalizationServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        return services.BuildServiceProvider();
    }

    private static string[] Arguments(string value) =>
        FormatArgument.Matches(value).Cast<Match>().Select(match => match.Groups[1].Value)
            .OrderBy(index => index, StringComparer.Ordinal).ToArray();

    private static Dictionary<string, string> ReadResources(string path)
    {
        var xml = XDocument.Load(path).Root ?? throw new InvalidOperationException("Missing resource root.");
        var entries = xml.Elements("data").Select(data => new
        {
            Key = (string?)data.Attribute("name") ?? "",
            Value = (string?)data.Element("value") ?? ""
        }).ToArray();
        Assert.All(entries, entry => Assert.False(string.IsNullOrEmpty(entry.Key)));
        Assert.Equal(entries.Length, entries.Select(entry => entry.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        return entries.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
    }

    private static string ResourcePath(string root, string sourcePath, string type)
    {
        const string prefix = "FullWorth.Web.";
        var relative = type.StartsWith(prefix, StringComparison.Ordinal)
            ? type[prefix.Length..].Replace('.', Path.DirectorySeparatorChar)
            : Path.Combine(Path.GetRelativePath(Path.Combine(root, "FullWorth.Web"),
                Path.GetDirectoryName(sourcePath)!), type);
        return Path.Combine(root, "FullWorth.Web", "Resources", relative + ".es.resx");
    }

    private static string RepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "FullWorth.slnx")))
                return directory.FullName;
        throw new InvalidOperationException("FullWorth repository root could not be located.");
    }
}
