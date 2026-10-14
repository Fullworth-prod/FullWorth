namespace FullWorth.API.Services.Statements;

/// <summary>Matches the existing PostgreSQL numeric(18,2) financial columns.</summary>
internal static class StatementMoneyStoragePolicy
{
    internal const decimal MaximumAmount = 9_999_999_999_999_999.99m;

    internal static bool IsSupportedSigned(decimal amount) =>
        amount >= -MaximumAmount && amount <= MaximumAmount &&
        decimal.Round(amount, 2, MidpointRounding.AwayFromZero) == amount;

    internal static bool IsSupportedTotal(decimal amount) =>
        amount >= 0m && IsSupportedSigned(amount);
}
