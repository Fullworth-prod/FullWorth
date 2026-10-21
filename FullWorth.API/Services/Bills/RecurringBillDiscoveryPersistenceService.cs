using FullWorth.API.Data;
using FullWorth.Core.Formatting;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Contracts;
using FullWorth.Core.Services;
using Microsoft.EntityFrameworkCore;

using BillCategory =
    FullWorth.Core.Models.BillCategory;

using CoreBankTransaction =
    FullWorth.Core.Models.BankTransaction;

namespace FullWorth.API.Services.Bills;

public sealed class RecurringBillDiscoveryPersistenceService
{
    private static readonly HashSet<string> HardRejectedPrimaryCategories =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "BANK_FEES", "INCOME", "LOAN_DISBURSEMENTS", "TRANSFER_IN", "TRANSFER_OUT"
        };

    private static readonly HashSet<string>
        StrongFallbackRejectedPrimaryCategories =
            new(
                StringComparer.OrdinalIgnoreCase)
            {
                "BANK_FEES",
                "FOOD_AND_DRINK",
                "GOVERNMENT_AND_NON_PROFIT",
                "HOME_IMPROVEMENT",
                "INCOME",
                "LOAN_DISBURSEMENTS",
                "LOAN_PAYMENTS",
                "MEDICAL",
                "TRANSPORTATION",
                "TRAVEL",
                "TRANSFER_IN",
                "TRANSFER_OUT"
            };

    private readonly FullWorthDbContext
        _dbContext;

    private readonly IBankTransactionDiscoveryGateway
        _transactionGateway;

    private readonly IBillTransactionAssociationGateway
        _associationGateway;

    private readonly BillStreamDiscoveryService
        _discoveryService;

    private readonly SupportedBillCategoryClassifier
        _categoryClassifier;

    private readonly BillMerchantNormalizer
        _merchantNormalizer;

    private readonly RecurringBillDiscoveryAlertService
        _discoveryAlertService;

    public RecurringBillDiscoveryPersistenceService(
        FullWorthDbContext dbContext,
        IBankTransactionDiscoveryGateway transactionGateway,
        IBillTransactionAssociationGateway associationGateway)
    {
        ArgumentNullException.ThrowIfNull(
            dbContext);

        ArgumentNullException.ThrowIfNull(
            transactionGateway);

        ArgumentNullException.ThrowIfNull(
            associationGateway);

        _dbContext =
            dbContext;

        _transactionGateway =
            transactionGateway;

        _associationGateway =
            associationGateway;

        _discoveryService =
            new BillStreamDiscoveryService();

        _categoryClassifier =
            new SupportedBillCategoryClassifier();

        _merchantNormalizer =
            new BillMerchantNormalizer();

        _discoveryAlertService =
            new RecurringBillDiscoveryAlertService(
                dbContext);
    }

    // Read-only analysis uses exactly the same deterministic candidate gates as
    // persistence, but intentionally does not sync accounts, stage associations,
    // create alerts, deactivate streams or save any database changes.
    public async Task<RecurringBillDiscoveryDiagnostics> GetDiagnosticsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "User ID is required.",
                nameof(userId));
        }

        var transactionSnapshots = await _transactionGateway
            .GetDiscoveryTransactionsAsync(userId, cancellationToken);

        var transactions = transactionSnapshots
            .Select(transaction => new DiscoveryTransaction(transaction, null))
            .ToList();

        return AnalyzeDiscovery(transactions, transactionSnapshots.Count).Diagnostics;
    }

    public async Task<RecurringBillDiscoveryPersistenceResult>
        DiscoverAndSaveAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
    {
        if (userId ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "User ID is required.",
                nameof(userId));
        }

        var transactionSnapshots =
            await _transactionGateway
                .GetDiscoveryTransactionsAsync(
                    userId,
                    cancellationToken);

        var transactionIds =
            transactionSnapshots
                .Select(transaction => transaction.TransactionId)
                .ToArray();

        var associationSnapshots =
            await _associationGateway.GetAsync(
                userId,
                transactionIds,
                cancellationToken);

        var billStreamIdByTransactionId =
            associationSnapshots
                .ToDictionary(
                    association => association.BankTransactionId,
                    association => association.BillStreamId);

        var persistedTransactions =
            transactionSnapshots
                .Select(
                    transaction =>
                        new DiscoveryTransaction(
                            transaction,
                            billStreamIdByTransactionId.TryGetValue(
                                transaction.TransactionId,
                                out var billStreamId)
                                ? billStreamId
                                : null))
                .ToList();

        var analysis = AnalyzeDiscovery(
            persistedTransactions,
            transactionSnapshots.Count);

        var acceptedDiscoveries = analysis.AcceptedDiscoveries;

        var existingStreams =
            await _dbContext.BillStreams
                .Where(
                    stream =>
                        stream.UserId ==
                            userId)
                .ToListAsync(
                    cancellationToken);

        var streamGroupsByProvider = existingStreams
            .GroupBy(stream => _merchantNormalizer.Normalize(stream.ProviderName),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(),
                StringComparer.OrdinalIgnoreCase);
        var ambiguousProviders = streamGroupsByProvider
            .Where(pair => pair.Value.Length > 1)
            .Select(pair => pair.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existingStreamsByProvider = streamGroupsByProvider
            .Where(pair => pair.Value.Length == 1)
            .ToDictionary(pair => pair.Key, pair => pair.Value[0],
                StringComparer.OrdinalIgnoreCase);

        var linkedTransactionsByStreamId =
            persistedTransactions
                .Where(
                    transaction =>
                        transaction.BillStreamId
                            .HasValue)
                .GroupBy(
                    transaction =>
                        transaction.BillStreamId!
                            .Value)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group.ToList());

        var discoveredProviderNames =
            acceptedDiscoveries
                .Select(
                    discovery =>
                        discovery.ProviderName)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        var discoveredCount = 0;

        var createdCount =
            0;

        var updatedCount =
            0;

        var deactivatedCount =
            0;

        var linkedTransactionCount =
            0;

        var unlinkedTransactionCount =
            0;

        var newBillAlertCount =
            0;

        var now =
            DateTimeOffset.UtcNow;

        // Incomplete bank imports can include some unrelated posted charges.
        // A partial window does not establish that an older recurring bill
        // ended. Require at least two approximate monthly cycles of eligible,
        // user-owned posted-debit history before considering deactivation.
        // This is a minimum coverage guard, not proof of provider cancellation.
        var firstEligibleDate = analysis.Diagnostics.FirstEligiblePostedDate;
        var lastEligibleDate = analysis.Diagnostics.LastEligiblePostedDate;
        var hasDeactivationCoverage =
            firstEligibleDate.HasValue &&
            lastEligibleDate.HasValue &&
            lastEligibleDate.Value.DayNumber - firstEligibleDate.Value.DayNumber >= 60;

        foreach (var existingStream in
                 existingStreams)
        {
            // Empty, pending-only, credit-only, and short-history syncs are
            // insufficient to deactivate streams or unlink prior evidence.
            if (!hasDeactivationCoverage)
            {
                continue;
            }

            if (existingStream.Source !=
                BillStreamSource.AutomaticDiscovery)
            {
                continue;
            }

            var normalizedExistingProvider =
                _merchantNormalizer.Normalize(
                    existingStream.ProviderName);

            if (ambiguousProviders.Contains(normalizedExistingProvider)) continue;

            if (discoveredProviderNames.Contains(
                    normalizedExistingProvider))
            {
                continue;
            }

            if (existingStream.IsActive)
            {
                existingStream.IsActive =
                    false;

                existingStream.UpdatedAtUtc =
                    now;

                deactivatedCount++;
            }

            if (!linkedTransactionsByStreamId
                .TryGetValue(
                    existingStream.Id,
                    out var linkedTransactions))
            {
                continue;
            }

            // Even an already-inactive stream must validate its source before
            // unlinking evidence; another context may just have confirmed it.
            _dbContext.Entry(existingStream).Property(stream => stream.Source).IsModified = true;

            foreach (var transaction in
                     linkedTransactions)
            {
                transaction.BillStreamId =
                    null;

                unlinkedTransactionCount++;
            }
        }

        foreach (var discovery in
                 acceptedDiscoveries)
        {
            existingStreamsByProvider
                .TryGetValue(
                    discovery.ProviderName,
                    out var persistedStream);

            // A normalized name is not an obligation identifier. Preserve both
            // duplicate streams and existing associations until the user resolves them.
            if (ambiguousProviders.Contains(discovery.ProviderName) ||
                discovery.Transactions.Any(transaction =>
                    transaction.OriginalBillStreamId.HasValue &&
                    transaction.OriginalBillStreamId != persistedStream?.Id))
            {
                continue;
            }

            discoveredCount++;

            if (persistedStream is
                null)
            {
                persistedStream =
                    new BillStreamEntity
                    {
                        UserId =
                            userId,

                        ProviderName =
                            GetMerchantName(
                                discovery.Transactions[0]),

                        Category =
                            discovery.Category,

                        Source =
                            BillStreamSource.AutomaticDiscovery,

                        IsActive =
                            true,

                        CreatedAtUtc =
                            now,

                        UpdatedAtUtc =
                            now
                    };

                _dbContext.BillStreams.Add(
                    persistedStream);

                existingStreams.Add(
                    persistedStream);

                existingStreamsByProvider[
                    discovery.ProviderName] =
                        persistedStream;

                createdCount++;

                /*
                 * The alert is generated only for a genuinely new
                 * automatically discovered Bill Stream.
                 *
                 * Reactivation or normal rediscovery of an existing
                 * stream does not create another notification.
                 */
                var alertCreated =
                    await _discoveryAlertService
                        .EnsureNewBillAlertAsync(
                            userId,
                            persistedStream,
                            discovery.Transactions.Count,
                            now,
                            cancellationToken,
                            newlyAddedStream:
                                true);

                if (alertCreated)
                {
                    newBillAlertCount++;
                }
            }
            else
            {
                var changed =
                    false;

                var wasAlreadyLinked =
                    discovery.Transactions
                        .Any(
                            transaction =>
                                transaction.BillStreamId ==
                                persistedStream.Id);

                if (persistedStream.Source ==
                        BillStreamSource.Unknown &&
                    wasAlreadyLinked)
                {
                    persistedStream.Source =
                        BillStreamSource.AutomaticDiscovery;

                    changed =
                        true;
                }

                if (persistedStream.Category ==
                        BillCategory.Unknown &&
                    discovery.Category !=
                        BillCategory.Unknown)
                {
                    persistedStream.Category =
                        discovery.Category;

                    changed =
                        true;
                }

                if (!persistedStream.IsActive)
                {
                    persistedStream.IsActive =
                        true;

                    changed =
                        true;
                }

                if (changed)
                {
                    persistedStream.UpdatedAtUtc =
                        now;

                    updatedCount++;
                }
            }

            foreach (var transaction in
                     discovery.Transactions)
            {
                if (transaction.BillStreamId ==
                    persistedStream.Id)
                {
                    continue;
                }

                transaction.BillStreamId =
                    persistedStream.Id;

                linkedTransactionCount++;
            }
        }

        var linkAssignments =
            persistedTransactions
                .Where(
                    transaction =>
                        transaction.OriginalBillStreamId !=
                            transaction.BillStreamId)
                .Select(
                    transaction =>
                        new BillTransactionAssociationAssignment(
                            transaction.Id,
                            transaction.OriginalBillStreamId,
                            transaction.BillStreamId))
                .ToArray();

        try
        {
            await _associationGateway.StageAssignmentsAsync(
                userId, linkAssignments, now, cancellationToken);
            // Bill streams, alerts, and links commit in the same owning unit of work.
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Secondary reconciliation must never replay staged writes after a
            // failed assignment, cancellation, database error, or concurrency conflict.
            _dbContext.ChangeTracker.Clear();
            throw;
        }

        return new RecurringBillDiscoveryPersistenceResult(
            TransactionsAnalyzed:
                analysis.TransactionsAnalyzed,

            RecurringCandidatesDetected:
                analysis.DetectedMonthlyCandidates,

            RecurringCandidatesRejected:
                analysis.DetectedMonthlyCandidates - discoveredCount,

            BillsDiscovered:
                discoveredCount,

            BillStreamsCreated:
                createdCount,

            BillStreamsUpdated:
                updatedCount,

            BillStreamsDeactivated:
                deactivatedCount,

            TransactionsLinked:
                linkedTransactionCount,

            TransactionsUnlinked:
                unlinkedTransactionCount,

            NewBillAlertsCreated:
                newBillAlertCount)
        {
            Diagnostics = analysis.Diagnostics
        };
    }

    private DiscoveryAnalysis AnalyzeDiscovery(
        IReadOnlyList<DiscoveryTransaction> persistedTransactions,
        int importedTransactionCount)
    {
        /*
         * Do not discard a transaction merely because Plaid placed it in a
         * category FullWorth does not currently recognize.
         *
         * Cadence is primary evidence that a charge is recurring. Category
         * evidence is evaluated after recurrence is established so a stable
         * subscription is not invisible just because its upstream category
         * is broad or missing.
         */
        var candidateTransactionsByProvider =
            new Dictionary<
                string,
                List<DiscoveryTransaction>>(
                    StringComparer.OrdinalIgnoreCase);

        var coreTransactions =
            new List<CoreBankTransaction>(
                persistedTransactions.Count);

        var excludedPendingOrNonDebit = 0;
        var excludedMissingMerchant = 0;

        foreach (var transaction in
                 persistedTransactions)
        {
            if (transaction.IsPending ||
                transaction.Amount <=
                    0m)
            {
                excludedPendingOrNonDebit++;
                continue;
            }

            var normalizedMerchantName =
                GetNormalizedMerchantName(
                    transaction);

            if (string.IsNullOrWhiteSpace(
                    normalizedMerchantName))
            {
                excludedMissingMerchant++;
                continue;
            }

            if (!candidateTransactionsByProvider
                .TryGetValue(
                    normalizedMerchantName,
                    out var providerTransactions))
            {
                providerTransactions =
                    [];

                candidateTransactionsByProvider.Add(
                    normalizedMerchantName,
                    providerTransactions);
            }

            providerTransactions.Add(
                transaction);

            coreTransactions.Add(
                ToCoreTransaction(
                    transaction,
                    normalizedMerchantName));
        }

        var detectedStreams =
            _discoveryService.Discover(
                coreTransactions);

        var acceptedDiscoveries =
            new List<AcceptedRecurringDiscovery>();

        // Aggregate only user-owned counts, never raw merchant or account data.
        var groupsWithOneObservation = candidateTransactionsByProvider.Values
            .Count(group => group.Count == 1);
        var groupsWithMultipleObservations = candidateTransactionsByProvider.Values
            .Count(group => group.Count >= 2);
        var groupsWithoutMonthlyEvidence = Math.Max(
            0, groupsWithMultipleObservations - detectedStreams.Count);
        var rejectedExplicitFallbackCategory = 0;
        var rejectedTwoObservationEvidence = 0;
        var rejectedOtherWeakEvidence = 0;
        var rejectedCurrencyAmbiguity = 0;

        foreach (var detectedStream in
                 detectedStreams)
        {
            if (!candidateTransactionsByProvider
                .TryGetValue(
                    detectedStream.ProviderName,
                    out var matchingTransactions))
            {
                continue;
            }

            var currencyUnits = matchingTransactions.Select(transaction =>
                FinancialAmountFormatter.TryNormalizeCurrency(transaction.CurrencyCode, out var code)
                    ? code : null).Distinct().Count();
            if (currencyUnits > 1)
            {
                rejectedCurrencyAmbiguity++;
                continue;
            }

            // Explicit transfer/income/loan evidence must not be overridden by a
            // familiar merchant name or another charge's supported category.
            if (matchingTransactions.Any(transaction =>
                HardRejectedPrimaryCategories.Contains(
                    transaction.CategoryPrimary?.Trim() ?? string.Empty) ||
                (string.Equals(transaction.CategoryPrimary?.Trim(), "LOAN_PAYMENTS",
                    StringComparison.OrdinalIgnoreCase) &&
                    ContainsAny(transaction.CategoryDetailed, "CASH_ADVANCES", "EWA"))))
            {
                rejectedExplicitFallbackCategory++;
                continue;
            }

            var resolvedCategory =
                ResolveBillCategory(
                    matchingTransactions);

            /*
             * The core discovery layer also contains deterministic
             * provider-name knowledge for known billers such as
             * Black Hills Energy, Midco, and Verizon.
             *
             * Preserve that evidence when Plaid's category metadata is
             * missing or outside FullWorth's supported category map. This is
             * especially important for variable-amount utilities, which
             * intentionally fail the generic stable-amount fallback.
             */
            if (resolvedCategory ==
                    BillCategory.Unknown &&
                detectedStream.Category !=
                    BillCategory.Unknown)
            {
                resolvedCategory =
                    detectedStream.Category;
            }

            /*
             * A brand-new monthly subscription may only have two posted
             * charges in the available history. Promote that early only when
             * the transactions contain explicit subscription/digital evidence
             * and are not in a category we deliberately reject. Generic
             * two-off merchant repeats remain candidates only and are not
             * persisted.
             */
            if (resolvedCategory ==
                    BillCategory.Unknown &&
                matchingTransactions.Count ==
                    2 &&
                HasStrongTwoOccurrenceSubscriptionEvidence(
                    matchingTransactions))
            {
                resolvedCategory =
                    BillCategory.Other;
            }

            if (resolvedCategory ==
                    BillCategory.Unknown &&
                IsStrongUnclassifiedRecurringBill(
                    matchingTransactions))
            {
                resolvedCategory =
                    BillCategory.Other;
            }

            if (resolvedCategory ==
                BillCategory.Unknown)
            {
                if (matchingTransactions.Any(IsExplicitlyRejectedFallbackCategory))
                {
                    rejectedExplicitFallbackCategory++;
                }
                else if (matchingTransactions.Count == 2)
                {
                    rejectedTwoObservationEvidence++;
                }
                else
                {
                    rejectedOtherWeakEvidence++;
                }

                continue;
            }

            acceptedDiscoveries.Add(
                new AcceptedRecurringDiscovery(
                    detectedStream.ProviderName,
                    matchingTransactions,
                    resolvedCategory));
        }
        var diagnostics = new RecurringBillDiscoveryDiagnostics(
                ImportedTransactions: importedTransactionCount,
                ExcludedPendingOrNonDebitTransactions: excludedPendingOrNonDebit,
                ExcludedMissingMerchantTransactions: excludedMissingMerchant,
                EligiblePostedTransactions: coreTransactions.Count,
                FirstEligiblePostedDate: coreTransactions.Count == 0
                    ? null : coreTransactions.Min(t => t.PostedDate),
                LastEligiblePostedDate: coreTransactions.Count == 0
                    ? null : coreTransactions.Max(t => t.PostedDate),
                SingleObservationGroups: groupsWithOneObservation,
                GroupsWithoutSupportedMonthlyCadence: groupsWithoutMonthlyEvidence,
                DetectedMonthlyCandidates: detectedStreams.Count,
                RejectedExplicitFallbackCategories: rejectedExplicitFallbackCategory,
                RejectedInsufficientTwoObservationEvidence: rejectedTwoObservationEvidence,
                RejectedOtherWeakOrVariableEvidence: rejectedOtherWeakEvidence,
                AcceptedCandidates: acceptedDiscoveries.Count)
        {
            RejectedCurrencyAmbiguity = rejectedCurrencyAmbiguity
        };

        return new DiscoveryAnalysis(
            coreTransactions.Count,
            detectedStreams.Count,
            acceptedDiscoveries,
            diagnostics);
    }

    private sealed record DiscoveryAnalysis(
        int TransactionsAnalyzed,
        int DetectedMonthlyCandidates,
        IReadOnlyList<AcceptedRecurringDiscovery> AcceptedDiscoveries,
        RecurringBillDiscoveryDiagnostics Diagnostics);

    private BillCategory ResolveBillCategory(
        IReadOnlyCollection<DiscoveryTransaction>
            transactions)
    {
        foreach (var transaction in
                 transactions)
        {
            if (_categoryClassifier.TryClassify(
                    transaction.CategoryPrimary,
                    transaction.CategoryDetailed,
                    out var category))
            {
                return category;
            }
        }

        return BillCategory.Unknown;
    }

    private static bool HasStrongTwoOccurrenceSubscriptionEvidence(
        IReadOnlyCollection<DiscoveryTransaction>
            transactions)
    {
        if (transactions.Count !=
            2)
        {
            return false;
        }

        return transactions.All(
                   transaction =>
                       !IsExplicitlyRejectedFallbackCategory(
                           transaction)) &&
               transactions.Any(
                   HasSubscriptionEvidence);
    }

    private static bool IsStrongUnclassifiedRecurringBill(
        IReadOnlyCollection<DiscoveryTransaction>
            transactions)
    {
        if (transactions.Count < 3)
        {
            return false;
        }

        foreach (var transaction in
                 transactions)
        {
            if (IsExplicitlyRejectedFallbackCategory(
                    transaction))
            {
                return false;
            }
        }

        var averageAmount =
            transactions.Average(
                transaction =>
                    transaction.Amount);

        if (averageAmount <= 0m)
        {
            return false;
        }

        var minimumAmount =
            transactions.Min(
                transaction =>
                    transaction.Amount);

        var maximumAmount =
            transactions.Max(
                transaction =>
                    transaction.Amount);

        var allowedVariation =
            Math.Max(
                1.00m,
                decimal.Round(
                    averageAmount * 0.05m,
                    2,
                    MidpointRounding.AwayFromZero));

        return maximumAmount - minimumAmount <=
            allowedVariation;
    }

    private static bool IsExplicitlyRejectedFallbackCategory(
        DiscoveryTransaction transaction)
    {
        var primary =
            transaction.CategoryPrimary?
                .Trim();

        if (string.IsNullOrWhiteSpace(
                primary))
        {
            return false;
        }

        if (StrongFallbackRejectedPrimaryCategories.Contains(
                primary))
        {
            return true;
        }

        if (string.Equals(
                primary,
                "GENERAL_MERCHANDISE",
                StringComparison.OrdinalIgnoreCase))
        {
            return !HasSubscriptionEvidence(
                transaction);
        }

        if (string.Equals(
                primary,
                "PERSONAL_CARE",
                StringComparison.OrdinalIgnoreCase))
        {
            return !ContainsAny(
                transaction.CategoryDetailed,
                "GYM",
                "FITNESS",
                "MEMBERSHIP",
                "SUBSCRIPTION");
        }

        if (string.Equals(
                primary,
                "GENERAL_SERVICES",
                StringComparison.OrdinalIgnoreCase) &&
            ContainsAny(
                transaction.CategoryDetailed,
                "AUTOMOTIVE"))
        {
            return true;
        }

        return false;
    }

    private static bool HasSubscriptionEvidence(
        DiscoveryTransaction transaction)
    {
        return ContainsAny(
                   transaction.CategoryDetailed,
                   "SUBSCRIPTION",
                   "MEMBERSHIP",
                   "DIGITAL",
                   "SOFTWARE",
                   "CLOUD") ||
               ContainsAny(
                   transaction.MerchantName,
                   "SUBSCRIPTION",
                   "MEMBERSHIP",
                   " BILL",
                   "BILL ") ||
               ContainsAny(
                   transaction.Name,
                   "SUBSCRIPTION",
                   "MEMBERSHIP",
                   " BILL",
                   "BILL ");
    }

    private static bool ContainsAny(
        string? value,
        params string[] candidates)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return false;
        }

        return candidates.Any(
            candidate =>
                value.Contains(
                    candidate,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static CoreBankTransaction ToCoreTransaction(
        DiscoveryTransaction transaction,
        string normalizedMerchantName)
    {
        return new CoreBankTransaction(
            merchantName:
                normalizedMerchantName,

            postedDate:
                transaction.PostedDate,

            amount:
                transaction.Amount,

            isPending:
                transaction.IsPending);
    }

    private string GetNormalizedMerchantName(
        DiscoveryTransaction transaction)
    {
        return _merchantNormalizer.Normalize(
            GetMerchantName(
                transaction));
    }

    private static string GetMerchantName(
        DiscoveryTransaction transaction)
    {
        var merchantName =
            string.IsNullOrWhiteSpace(
                transaction.MerchantName)
                ? transaction.Name
                : transaction.MerchantName;

        return merchantName.Trim();
    }

    private sealed record AcceptedRecurringDiscovery(
        string ProviderName,
        IReadOnlyList<DiscoveryTransaction> Transactions,
        BillCategory Category);

    private sealed class DiscoveryTransaction
    {
        public DiscoveryTransaction(
            BankTransactionDiscoveryRecord source,
            Guid? billStreamId)
        {
            Id = source.TransactionId;
            OriginalBillStreamId = billStreamId;
            BillStreamId = billStreamId;
            Name = source.Name;
            MerchantName = source.MerchantName;
            Amount = source.Amount;
            PostedDate = source.PostedDate;
            IsPending = source.IsPending;
            CategoryPrimary = source.CategoryPrimary;
            CategoryDetailed = source.CategoryDetailed;
            CurrencyCode = source.CurrencyCode;
        }

        public Guid Id { get; }

        public Guid? OriginalBillStreamId { get; }

        public Guid? BillStreamId { get; set; }

        public string Name { get; }

        public string? MerchantName { get; }

        public decimal Amount { get; }

        public DateOnly PostedDate { get; }

        public bool IsPending { get; }

        public string? CategoryPrimary { get; }

        public string? CategoryDetailed { get; }

        public string? CurrencyCode { get; }
    }
}

public sealed record RecurringBillDiscoveryPersistenceResult(
    int TransactionsAnalyzed,
    int RecurringCandidatesDetected,
    int RecurringCandidatesRejected,
    int BillsDiscovered,
    int BillStreamsCreated,
    int BillStreamsUpdated,
    int BillStreamsDeactivated,
    int TransactionsLinked,
    int TransactionsUnlinked,
    int NewBillAlertsCreated)
{
    // Deliberately summary-only. Do not expose transaction/provider identifiers in
    // unauthenticated responses, telemetry, CI logs or public issue comments.
    public RecurringBillDiscoveryDiagnostics? Diagnostics { get; init; }
}

public sealed record RecurringBillDiscoveryDiagnostics(
    int ImportedTransactions,
    int ExcludedPendingOrNonDebitTransactions,
    int ExcludedMissingMerchantTransactions,
    int EligiblePostedTransactions,
    DateOnly? FirstEligiblePostedDate,
    DateOnly? LastEligiblePostedDate,
    int SingleObservationGroups,
    int GroupsWithoutSupportedMonthlyCadence,
    int DetectedMonthlyCandidates,
    int RejectedExplicitFallbackCategories,
    int RejectedInsufficientTwoObservationEvidence,
    int RejectedOtherWeakOrVariableEvidence,
    int AcceptedCandidates)
{
    public int RejectedCurrencyAmbiguity { get; init; }
}
