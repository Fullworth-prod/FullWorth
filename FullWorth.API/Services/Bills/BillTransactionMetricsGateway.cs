using FullWorth.API.Data;
using FullWorth.Core.Formatting;
using FullWorth.Core.Models;
using FullWorth.Core.Services;
using FullWorth.API.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.API.Services.Bills;

public sealed class BillTransactionMetricsGateway(
    FullWorthDbContext dbContext,
    IBankTransactionMetricFactsGateway transactionFactsGateway)
    : IBankTransactionBillMetricsGateway
{
    public async Task<IReadOnlyDictionary<Guid, BankTransactionBillStreamMetrics>>
        GetMetricsAsync(
            Guid userId,
            IReadOnlyCollection<Guid> billStreamIds,
            CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "User ID is required.",
                nameof(userId));
        }

        ArgumentNullException.ThrowIfNull(billStreamIds);

        if (billStreamIds.Count == 0)
        {
            return new Dictionary<Guid, BankTransactionBillStreamMetrics>();
        }

        var ids = billStreamIds
            .Distinct()
            .ToArray();

        if (ids.Any(id => id == Guid.Empty))
        {
            throw new ArgumentException(
                "Bill Stream IDs must not be empty.",
                nameof(billStreamIds));
        }

        var associations =
            await dbContext.BillTransactionAssociations
                .AsNoTracking()
                .Where(
                    association =>
                        association.UserId == userId &&
                        ids.Contains(association.BillStreamId))
                .Select(
                    association =>
                        new
                        {
                            association.BankTransactionId,
                            association.BillStreamId
                        })
                .ToListAsync(cancellationToken);

        if (associations.Count == 0)
        {
            return new Dictionary<Guid, BankTransactionBillStreamMetrics>();
        }

        var facts =
            await transactionFactsGateway.GetAsync(
                userId,
                associations
                    .Select(association => association.BankTransactionId)
                    .ToArray(),
                cancellationToken);

        var billStreamIdByTransactionId =
            associations.ToDictionary(
                association => association.BankTransactionId,
                association => association.BillStreamId);

        return facts
            .Where(
                fact =>
                    fact.Amount > 0m &&
                    billStreamIdByTransactionId.ContainsKey(
                        fact.TransactionId))
            .GroupBy(
                fact =>
                    billStreamIdByTransactionId[fact.TransactionId])
            .Select(
                group =>
                {
                    var ordered = group
                        .OrderByDescending(fact => fact.PostedDate)
                        .ThenByDescending(fact => fact.CreatedAtUtc)
                        .ThenByDescending(fact => fact.TransactionId)
                        .ToArray();

                    var currencies = ordered.Select(fact =>
                        FinancialAmountFormatter.TryNormalizeCurrency(fact.CurrencyCode, out var code)
                            ? code : null).Distinct().ToArray();
                    var comparable = currencies.Length == 1 && currencies[0] is not null;
                    // Mixed or partially unknown units cannot support a monetary comparison.
                    if (currencies.Length != 1) return null;

                    return new BankTransactionBillStreamMetrics(
                        group.Key,
                        ordered[0].Amount,
                        !comparable || ordered.Length <= 1
                            ? 0m
                            : ordered
                                .Skip(1)
                                .Average(fact => fact.Amount))
                    {
                        CurrencyCode = currencies[0],
                        HasMonthlyCadenceEvidence = comparable && new RecurringBillDetectionService()
                            .Detect(ordered.Select(fact => new BankTransaction(
                                merchantName: "Bill",
                                postedDate: fact.PostedDate,
                                amount: fact.Amount,
                                isPending: false))).Count > 0
                    };
                })
            .Where(metric => metric is not null)
            .ToDictionary(metric => metric!.BillStreamId, metric => metric!);
    }
}
