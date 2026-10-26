using System.Globalization;

namespace FullWorth.Core.Subscriptions;

public sealed record SubscriptionFactInput(
    string Field, string Value, DateTimeOffset ObservedAtUtc, DateTimeOffset ValidUntilUtc);
public sealed record SubscriptionEvidenceRequest(
    Guid SubmissionId, Guid ExpectedRevision, string Source, Guid? SourceUploadId,
    IReadOnlyList<SubscriptionFactInput> Facts);
public sealed record CreateSubscriptionReviewRequest(
    Guid Id, string MerchantName, string PlanLabel, string BillingParty,
    Guid? SourceTransactionId = null, Guid? BillStreamId = null);
public sealed record SubscriptionObservation(
    Guid SubmissionId, string Source, Guid? SourceUploadId, DateTimeOffset RecordedAtUtc,
    SubscriptionFactInput Fact);
public sealed record SubscriptionFactResult(
    string Field, string? Value, string State, IReadOnlyList<SubscriptionObservation> Evidence);
public sealed record SubscriptionVerificationResult(
    Guid Id, Guid Revision, string MerchantName, string PlanLabel, string BillingParty,
    Guid? SourceTransactionId, Guid? BillStreamId, bool NeedsReview,
    IReadOnlyList<SubscriptionFactResult> Facts,
    string MerchantVerification = "Unavailable");

/// <summary>Evidence-only view. Never creates ledger entries or forecast obligations.</summary>
public static class SubscriptionVerification
{
    public static readonly IReadOnlyList<string> Fields = Array.AsReadOnly(new[]
    {
        "ServiceStatus", "Renewal", "BillingArrangement", "PaidThrough",
        "NextChargeDate", "NextChargeAmount", "CurrencyCode"
    });

    public static bool TryNormalize(
        IReadOnlyList<SubscriptionFactInput>? facts, DateTimeOffset now,
        out IReadOnlyList<SubscriptionFactInput> normalized)
    {
        normalized = [];
        if (facts is null || facts.Count is < 1 or > 7 ||
            facts.Any(f => f is null) ||
            facts.Select(f => f.Field).Distinct(StringComparer.Ordinal).Count() != facts.Count)
            return false;
        var output = new List<SubscriptionFactInput>();
        foreach (var fact in facts)
        {
            if (!Fields.Contains(fact.Field) || string.IsNullOrWhiteSpace(fact.Value) ||
                fact.Value.Length > 40 || fact.ObservedAtUtc == default ||
                fact.ObservedAtUtc > now.AddMinutes(5) ||
                fact.ValidUntilUtc <= fact.ObservedAtUtc ||
                fact.ValidUntilUtc > fact.ObservedAtUtc.AddDays(31))
                return false;
            var value = fact.Value.Trim();
            var valid = fact.Field switch
            {
                "ServiceStatus" => value is "Active" or "Paused" or "Ended" or "Unknown",
                "Renewal" => value is "Yes" or "No" or "Unknown",
                "BillingArrangement" => value is "Recurring" or "Prepaid" or "FixedTerm" or "OneOff" or "Trial" or "Unknown",
                "PaidThrough" or "NextChargeDate" => value == "Unknown" ||
                    DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out _),
                "NextChargeAmount" => value == "Unknown" ||
                    decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture,
                        out var amount) && amount > 0m && amount <= 999999999m &&
                    decimal.Round(amount, 4) == amount,
                // Currency is explicit, never guessed from UI culture. Restrict to ISO-style
                // codes; no conversion or arithmetic is performed by this feature.
                "CurrencyCode" => value == "Unknown" ||
                    value.Length == 3 && value.All(c => c is >= 'A' and <= 'Z'),
                _ => false
            };
            if (!valid) return false;
            if (fact.Field == "NextChargeAmount" && value != "Unknown")
                value = decimal.Parse(value, CultureInfo.InvariantCulture).ToString("0.####", CultureInfo.InvariantCulture);
            output.Add(fact with
            {
                Value = value, ObservedAtUtc = fact.ObservedAtUtc.ToUniversalTime(),
                ValidUntilUtc = fact.ValidUntilUtc.ToUniversalTime()
            });
        }
        // Amount and denomination are one claim and must have matching evidence age.
        var amountFact = output.SingleOrDefault(f => f.Field == "NextChargeAmount");
        var currencyFact = output.SingleOrDefault(f => f.Field == "CurrencyCode");
        if ((amountFact is null) != (currencyFact is null) ||
            amountFact is not null && currencyFact is not null &&
            ((amountFact.Value == "Unknown") != (currencyFact.Value == "Unknown") ||
             amountFact.ObservedAtUtc != currencyFact.ObservedAtUtc ||
             amountFact.ValidUntilUtc != currencyFact.ValidUntilUtc))
            return false;
        normalized = output.OrderBy(f => f.Field, StringComparer.Ordinal).ToArray();
        return true;
    }

    public static IReadOnlyList<SubscriptionFactResult> Resolve(
        IEnumerable<SubscriptionObservation> observations, DateTimeOffset now)
    {
        var materialized = observations.ToArray();
        var resolved = Fields.Select(field =>
        {
            var evidence = materialized.Where(o => o.Fact.Field == field)
                .OrderByDescending(o => o.RecordedAtUtc).ThenBy(o => o.SubmissionId).ToArray();
            // A later user entry supersedes only that source, never a separate source.
            // An older document uploaded later must not replace a newer observation.
            var latestBySource = evidence.GroupBy(o => o.Source)
                .Select(g => g.OrderByDescending(o => o.Fact.ObservedAtUtc)
                    .ThenByDescending(o => o.RecordedAtUtc).ThenBy(o => o.SubmissionId).First())
                .ToArray();
            var fresh = latestBySource.Where(o =>
                o.Fact.ObservedAtUtc <= now.AddMinutes(5) && o.Fact.ValidUntilUtc > now).ToArray();
            var values = fresh.Select(o => o.Fact.Value).Distinct(StringComparer.Ordinal).ToArray();
            var state = evidence.Length == 0 ? "Unknown" :
                fresh.Length == 0 ? "Stale" :
                values.Length > 1 ? "Conflict" :
                values[0] == "Unknown" ? "Unknown" : "Current";
            var value = state == "Current" ? values[0] : null;
            if (field == "NextChargeDate" && value is not null &&
                DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture) <
                DateOnly.FromDateTime(now.UtcDateTime))
            {
                state = "Stale";
                value = null;
            }
            return new SubscriptionFactResult(field, value, state, evidence);
        }).ToArray();
        var amountIndex = Array.FindIndex(resolved, f => f.Field == "NextChargeAmount");
        var currency = resolved.Single(f => f.Field == "CurrencyCode");
        if (resolved[amountIndex].State == "Current" && currency.State != "Current")
            resolved[amountIndex] = resolved[amountIndex] with
            {
                Value = null, State = currency.State == "Conflict" ? "Conflict" : "Unknown"
            };
        return resolved;
    }

    public static bool HasInconsistentClaims(IReadOnlyList<SubscriptionFactResult> facts)
    {
        string? Get(string field) => facts.Single(f => f.Field == field).Value;
        return facts.Any(f => f.State is "Conflict" or "Stale") ||
            Get("Renewal") == "Yes" &&
                (Get("ServiceStatus") == "Ended" || Get("BillingArrangement") == "OneOff") ||
            Get("Renewal") == "No" && Get("NextChargeDate") is not null;
    }
}
