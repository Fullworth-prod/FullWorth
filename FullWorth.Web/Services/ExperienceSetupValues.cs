namespace FullWorth.Web.Services;

/// <summary>Presentation-only preferences. No account authorization or financial state.</summary>
public sealed record ExperienceSetupValues(string Language, string Theme, string TextSize,
    bool HighContrast, bool ReduceMotion, IReadOnlyList<string> Focus)
{
    public static readonly IReadOnlyList<string> FocusOrder = Array.AsReadOnly(new[]
        { "BillChanges", "Spending", "RecurringCosts", "AccountOverview", "Statements" });

    public bool Matches(ExperienceSetupValues other) =>
        Language == other.Language && Theme == other.Theme && TextSize == other.TextSize &&
        HighContrast == other.HighContrast && ReduceMotion == other.ReduceMotion &&
        Focus.OrderBy(value => value, StringComparer.Ordinal)
            .SequenceEqual(other.Focus.OrderBy(value => value, StringComparer.Ordinal));

    public static ExperienceSetupValues Read(ExperienceSetupResponse? response, bool requireComplete = false)
    {
        if (response is null || response.ExperienceSetupComplete is null ||
            response.HighContrastEnabled is null || response.ReduceMotionEnabled is null ||
            (requireComplete && response.ExperienceSetupComplete != true))
            throw new InvalidOperationException("Incomplete experience preference response.");

        var language = response.PreferredUiLanguage?.Trim().ToLowerInvariant() switch
        {
            "en" or "en-us" => "en-US",
            "es" or "es-es" or "es-us" => "es",
            _ => throw new InvalidOperationException("Invalid experience language.")
        };
        var theme = response.ThemePreference?.Trim().ToLowerInvariant() switch
        {
            "system" => "System", "light" => "Light", "dark" => "Dark",
            _ => throw new InvalidOperationException("Invalid experience theme.")
        };
        var textSize = response.TextSizePreference?.Trim().ToLowerInvariant() switch
        {
            "standard" => "Standard", "large" => "Large", "extralarge" or "extra-large" => "ExtraLarge",
            _ => throw new InvalidOperationException("Invalid experience text size.")
        };
        var focus = response.ExperienceFocus;
        if (focus is null || focus.Length > FocusOrder.Count ||
            focus.Any(value => !FocusOrder.Contains(value, StringComparer.Ordinal)) ||
            focus.Distinct(StringComparer.Ordinal).Count() != focus.Length ||
            (response.ExperienceSetupComplete == true && focus.Length == 0))
            throw new InvalidOperationException("Invalid experience focus.");
        // An incomplete new account can legitimately have no focus yet.
        return new(language, theme, textSize, response.HighContrastEnabled.Value,
            response.ReduceMotionEnabled.Value, Array.AsReadOnly(
                FocusOrder.Where(value => focus.Contains(value, StringComparer.Ordinal)).ToArray()));
    }
}

public sealed class ExperienceSetupResponse
{
    public bool? ExperienceSetupComplete { get; set; }
    public string? PreferredUiLanguage { get; set; }
    public string? ThemePreference { get; set; }
    public string? TextSizePreference { get; set; }
    public bool? HighContrastEnabled { get; set; }
    public bool? ReduceMotionEnabled { get; set; }
    public string[]? ExperienceFocus { get; set; }
}
