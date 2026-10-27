using FullWorth.Core.Models;

namespace FullWorth.Core.Services;

public sealed class BillStreamAnalysisService
{
    private readonly BillAnalysisService _billAnalysisService;

    public BillStreamAnalysisService()
    {
        _billAnalysisService =
            new BillAnalysisService();
    }

    public BillAnalysisResult? Analyze(
        BillStream billStream)
    {
        ArgumentNullException.ThrowIfNull(billStream);

        if (billStream.Statements.Count < 2)
        {
            return null;
        }

        var previousStatement =
            billStream.Statements[^2];

        var currentStatement =
            billStream.Statements[^1];

        // No supported comparison is an ordinary nullable outcome, not a
        // fabricated monthly change or a mixed-currency exception.
        if (!string.Equals(previousStatement.CurrencyCode, currentStatement.CurrencyCode,
                StringComparison.Ordinal) ||
            !MonthlyStatementPeriodPolicy.CanCompare(
                previousStatement.BillingPeriodStart, previousStatement.BillingPeriodEnd,
                currentStatement.BillingPeriodStart, currentStatement.BillingPeriodEnd))
        {
            return null;
        }

        var matchingTransaction =
            FindBestMatchingTransaction(
                billStream,
                currentStatement);

        if (matchingTransaction is not null)
        {
            return _billAnalysisService.Analyze(
                matchingTransaction,
                previousStatement,
                currentStatement);
        }

        return _billAnalysisService.Analyze(
            previousStatement,
            currentStatement);
    }

    private static BankTransaction? FindBestMatchingTransaction(
        BillStream billStream,
        BillStatement currentStatement)
    {
        return billStream.Transactions
            .Where(transaction =>
                !transaction.IsPending &&
                string.Equals(
                    transaction.MerchantName,
                    currentStatement.ProviderName,
                    StringComparison.OrdinalIgnoreCase))
            .OrderBy(transaction =>
                Math.Abs(
                    transaction.Amount -
                    currentStatement.TotalAmount.Amount))
            .ThenByDescending(transaction =>
                transaction.PostedDate)
            .FirstOrDefault();
    }
}