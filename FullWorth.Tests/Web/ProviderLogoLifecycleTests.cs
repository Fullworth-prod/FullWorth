using System.Reflection;
using Microsoft.AspNetCore.Components;
using FullWorth.Web.Components.Shared;

namespace FullWorth.Tests.Web;

public sealed class ProviderLogoLifecycleTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Call(ProviderLogo logo, string method, params object?[] args) =>
        typeof(ProviderLogo).GetMethod(method, Flags)!.Invoke(logo, args);
    private static T Field<T>(ProviderLogo logo, string name) =>
        (T)typeof(ProviderLogo).GetField(name, Flags)!.GetValue(logo)!;
    private static ProviderLogo Logo(string name)
    {
        var logo = new ProviderLogo();
        SetProvider(logo, name);
        return logo;
    }

    private static void SetProvider(ProviderLogo logo, string name)
    {
        ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(ProviderLogo.ProviderName)] = name
        }).SetParameterProperties(logo);
        Call(logo, "OnParametersSet");
    }

    [Fact]
    public void PendingLogo_HasNoLoadedImageUntilItsOwnLoadEvent()
    {
        var logo = Logo("Spotify");
        Assert.Null(Field<string?>(logo, "_loadedUrl"));
        var url = Field<string>(logo, "_currentUrl");
        var generation = Field<long>(logo, "_generation");
        Call(logo, "MarkLoaded", url, generation);
        Assert.Equal(url, Field<string>(logo, "_loadedUrl"));
    }

    [Fact]
    public void FailedLogo_KeepsFallbackWithoutRetryingOnOrdinaryRerender()
    {
        var logo = Logo("Spotify");
        var url = Field<string>(logo, "_currentUrl");
        var generation = Field<long>(logo, "_generation");
        Call(logo, "MarkUnavailable", url, generation);
        Call(logo, "OnParametersSet");
        Call(logo, "MarkLoaded", url, generation);
        Assert.Equal(url, Field<string>(logo, "_failedUrl"));
        Assert.Null(Field<string?>(logo, "_loadedUrl"));
        Assert.Equal(generation, Field<long>(logo, "_generation"));
    }

    [Fact]
    public void ProviderChange_RejectsStaleEventsEvenWhenReturningToOriginalProvider()
    {
        var logo = Logo("Spotify");
        var oldUrl = Field<string>(logo, "_currentUrl");
        var oldGeneration = Field<long>(logo, "_generation");
        SetProvider(logo, "Netflix");
        SetProvider(logo, "Spotify");
        Call(logo, "MarkUnavailable", oldUrl, oldGeneration);
        Call(logo, "MarkLoaded", oldUrl, oldGeneration);
        Assert.Null(Field<string?>(logo, "_failedUrl"));
        Assert.Null(Field<string?>(logo, "_loadedUrl"));
        Assert.True(Field<long>(logo, "_generation") > oldGeneration);
        Call(logo, "MarkLoaded", oldUrl, Field<long>(logo, "_generation"));
        Assert.Equal(oldUrl, Field<string>(logo, "_loadedUrl"));
    }

    [Fact]
    public void UnknownProvider_DropsPriorImageStateAndUsesItsOwnFallback()
    {
        var logo = Logo("Spotify");
        Call(logo, "MarkLoaded", Field<string>(logo, "_currentUrl"), Field<long>(logo, "_generation"));
        SetProvider(logo, "Unmapped test utility");
        Assert.Null(Field<string?>(logo, "_currentUrl"));
        Assert.Null(Field<string?>(logo, "_loadedUrl"));
        Assert.Null(Field<string?>(logo, "_failedUrl"));
    }
}
