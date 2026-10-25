using FullWorth.Core.Subscriptions;

namespace FullWorth.Tests.Services;

public sealed class SubscriptionVerificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    private static SubscriptionFactInput Fact(string field, string value, int age = 0, int valid = 30) =>
        new(field, value, Now.AddDays(-age), Now.AddDays(-age + valid));
    private static SubscriptionObservation Observation(SubscriptionFactInput fact,
        string source = "UserConfirmed", int recordedMinutes = 0) =>
        new(Guid.NewGuid(), source, null, Now.AddMinutes(recordedMinutes), fact);

    [Theory]
    [InlineData("ServiceStatus", "Active")]
    [InlineData("ServiceStatus", "Paused")]
    [InlineData("ServiceStatus", "Ended")]
    [InlineData("Renewal", "Yes")]
    [InlineData("Renewal", "No")]
    [InlineData("BillingArrangement", "Recurring")]
    [InlineData("BillingArrangement", "Prepaid")]
    [InlineData("BillingArrangement", "FixedTerm")]
    [InlineData("BillingArrangement", "OneOff")]
    [InlineData("PaidThrough", "2026-11-25")]
    [InlineData("NextChargeDate", "2026-11-25")]
    public void SupportedFacts_RetainIndependentMeaning(string field, string value)
    {
        Assert.True(SubscriptionVerification.TryNormalize([Fact(field, value)], Now, out var normalized));
        var result = SubscriptionVerification.Resolve(normalized.Select(f => Observation(f)), Now);
        Assert.Equal(value, result.Single(f => f.Field == field).Value);
        if (field != "Renewal") Assert.Null(result.Single(f => f.Field == "Renewal").Value);
    }

    [Theory]
    [InlineData("ServiceStatus", "Premium")]
    [InlineData("Renewal", "true")]
    [InlineData("Renewal", "yes")]
    [InlineData("BillingArrangement", "gift card")]
    [InlineData("PaidThrough", "11/25/2026")]
    [InlineData("NextChargeDate", "2026-02-30")]
    [InlineData("NextChargeAmount", "-1")]
    [InlineData("CurrencyCode", "usd")]
    [InlineData("MerchantVerified", "Yes")]
    public void UnsupportedClaims_AreRejected(string field, string value) =>
        Assert.False(SubscriptionVerification.TryNormalize([Fact(field, value)], Now, out _));

    [Fact]
    public void NoEvidence_DoesNotInferRecurrenceOrActiveAccess()
    {
        var result = SubscriptionVerification.Resolve([], Now);
        Assert.Equal(7, result.Count);
        Assert.All(result, f => { Assert.Null(f.Value); Assert.Equal("Unknown", f.State); });
    }

    [Theory]
    [InlineData(31, 30)]
    [InlineData(1, 1)]
    public void ExpiredEvidence_IsHistoricalNotCurrent(int age, int valid)
    {
        var result = SubscriptionVerification.Resolve([Observation(Fact("Renewal", "Yes", age, valid))], Now);
        var renewal = result.Single(f => f.Field == "Renewal");
        Assert.Null(renewal.Value);
        Assert.Equal("Stale", renewal.State);
        Assert.Single(renewal.Evidence);
    }

    [Fact]
    public void NewerSameSourceDecisionSupersedes_OlderReceiptDoesNotReplaceNewerDocument()
    {
        var result = SubscriptionVerification.Resolve([
            Observation(Fact("Renewal", "Yes", 3), recordedMinutes: -3),
            Observation(Fact("Renewal", "No"), recordedMinutes: -2),
            Observation(Fact("Renewal", "No"), "DocumentConfirmed", -1),
            Observation(Fact("Renewal", "Yes", 5), "DocumentConfirmed", 0)], Now);
        Assert.Equal("No", result.Single(f => f.Field == "Renewal").Value);
        Assert.Equal(4, result.Single(f => f.Field == "Renewal").Evidence.Count);
    }

    [Fact]
    public void ConflictingFreshSources_DoNotChooseAnUnsupportedWinner()
    {
        var result = SubscriptionVerification.Resolve([
            Observation(Fact("Renewal", "Yes")),
            Observation(Fact("Renewal", "No"), "DocumentConfirmed")], Now);
        var renewal = result.Single(f => f.Field == "Renewal");
        Assert.Null(renewal.Value);
        Assert.Equal("Conflict", renewal.State);
        Assert.True(SubscriptionVerification.HasInconsistentClaims(result));
    }

    [Theory]
    [InlineData("Prepaid", "Yes", false)]
    [InlineData("Prepaid", "No", false)]
    [InlineData("FixedTerm", "Unknown", false)]
    [InlineData("OneOff", "No", false)]
    [InlineData("OneOff", "Yes", true)]
    public void PrepaidAndAccess_DoNotImplyRenewal(string arrangement, string renewal, bool conflict)
    {
        var result = SubscriptionVerification.Resolve([
            Observation(Fact("ServiceStatus", "Active")),
            Observation(Fact("BillingArrangement", arrangement)),
            Observation(Fact("Renewal", renewal))], Now);
        Assert.Equal(conflict, SubscriptionVerification.HasInconsistentClaims(result));
    }

    [Fact]
    public void CancelledButAccessible_IsNotConvertedToEnded()
    {
        var result = SubscriptionVerification.Resolve([
            Observation(Fact("ServiceStatus", "Active")),
            Observation(Fact("Renewal", "No")),
            Observation(Fact("PaidThrough", "2026-11-01"))], Now);
        Assert.Equal("Active", result.Single(f => f.Field == "ServiceStatus").Value);
        Assert.Equal("No", result.Single(f => f.Field == "Renewal").Value);
        Assert.False(SubscriptionVerification.HasInconsistentClaims(result));
    }

    [Fact]
    public void PassedChargeDate_BecomesStaleRatherThanRollingForward()
    {
        var result = SubscriptionVerification.Resolve([Observation(Fact("NextChargeDate", "2026-10-24"))], Now);
        Assert.Equal("Stale", result.Single(f => f.Field == "NextChargeDate").State);
        Assert.Null(result.Single(f => f.Field == "NextChargeDate").Value);
    }

    [Fact]
    public void AmountAndCurrency_MustHaveTheSameEvidenceTime()
    {
        Assert.False(SubscriptionVerification.TryNormalize([Fact("NextChargeAmount", "10")], Now, out _));
        Assert.False(SubscriptionVerification.TryNormalize([
            Fact("NextChargeAmount", "10"), Fact("CurrencyCode", "USD", 1)], Now, out _));
        Assert.True(SubscriptionVerification.TryNormalize([
            Fact("NextChargeAmount", "10.5000"), Fact("CurrencyCode", "USD")], Now, out var normalized));
        Assert.Equal("10.5", normalized.Single(f => f.Field == "NextChargeAmount").Value);
    }

    [Fact]
    public void InvalidFreshnessDuplicateAndNullInputs_FailClosed()
    {
        Assert.False(SubscriptionVerification.TryNormalize(null, Now, out _));
        Assert.False(SubscriptionVerification.TryNormalize([null!], Now, out _));
        Assert.False(SubscriptionVerification.TryNormalize([Fact("Renewal", "Yes"), Fact("Renewal", "No")], Now, out _));
        Assert.False(SubscriptionVerification.TryNormalize([Fact("Renewal", "Yes", valid: 32)], Now, out _));
        Assert.False(SubscriptionVerification.TryNormalize([Fact("Renewal", "Yes", valid: 0)], Now, out _));
        Assert.False(SubscriptionVerification.TryNormalize([Fact("Renewal", "Yes") with { ObservedAtUtc = Now.AddDays(1) }], Now, out _));
    }
}
