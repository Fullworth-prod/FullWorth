using System.Threading.Channels;
using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FullWorth.API.Services.Statements;

public sealed class BillStatementProcessingSignal
{
    private readonly Channel<bool> _channel =
        Channel.CreateBounded<bool>(
            new BoundedChannelOptions(1)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropWrite,
                AllowSynchronousContinuations = false
            });

    public void Notify()
    {
        _channel.Writer.TryWrite(true);
    }

    public async ValueTask WaitAsync(
        CancellationToken cancellationToken)
    {
        await _channel.Reader.ReadAsync(cancellationToken);
    }
}

public sealed class BillStatementProcessingService
{
    private readonly FullWorthDbContext _dbContext;
    private readonly BillStatementDocumentTextReader _documentTextReader;
    private readonly IBillStatementExtractionService _extractionService;
    private readonly BillStatementValidationService _validationService;
    private readonly BillStatementPersistenceService _persistenceService;
    private readonly IBillStreamReadGateway _billStreamGateway;
    private readonly ILogger<BillStatementProcessingService> _logger;

    public BillStatementProcessingService(
        FullWorthDbContext dbContext,
        BillStatementDocumentTextReader documentTextReader,
        IBillStatementExtractionService extractionService,
        BillStatementValidationService validationService,
        BillStatementPersistenceService persistenceService,
        IBillStreamReadGateway billStreamGateway,
        ILogger<BillStatementProcessingService> logger)
    {
        _dbContext = dbContext;
        _documentTextReader = documentTextReader;
        _extractionService = extractionService;
        _validationService = validationService;
        _persistenceService = persistenceService;
        _billStreamGateway = billStreamGateway;
        _logger = logger;
    }

    public async Task<int> ProcessPendingBatchAsync(
        int maxItems,
        CancellationToken cancellationToken = default) =>
        (await ProcessPendingBatchDetailedAsync(
            maxItems, [], cancellationToken)).ConsideredCount;

    public async Task<BillStatementProcessingBatchResult> ProcessPendingBatchDetailedAsync(
        int maxItems,
        IReadOnlyCollection<Guid> deferredUploadIds,
        CancellationToken cancellationToken = default)
    {
        if (maxItems <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxItems));
        ArgumentNullException.ThrowIfNull(deferredUploadIds);

        // Query IDs without tracking so one failed DbContext update cannot
        // retain dirty entities when work continues to the next upload.
        var query = _dbContext.BillStatementUploads
            .AsNoTracking()
            .Where(upload =>
                upload.Status == BillStatementUploadStatus.Uploaded ||
                upload.Status == BillStatementUploadStatus.Processing);

        var excluded = deferredUploadIds.Distinct().ToArray();
        if (excluded.Length > 0)
            query = query.Where(upload => !excluded.Contains(upload.Id));

        var candidateIds = await query
            .OrderBy(upload => upload.CreatedAtUtc)
            .ThenBy(upload => upload.Id)
            .Take(maxItems)
            .Select(upload => upload.Id)
            .ToListAsync(cancellationToken);

        var failures = await BillStatementProcessingBatchRunner.RunAsync(
            candidateIds,
            async (id, token) =>
            {
                var upload = await _dbContext.BillStatementUploads
                    .SingleOrDefaultAsync(item => item.Id == id, token);
                if (upload is not null)
                    await ProcessUploadAsync(upload, token);
            },
            (id, ex) =>
            {
                _dbContext.ChangeTracker.Clear();
                _logger.LogError(
                    "Unexpected document processing failure for upload {UploadId}: {ExceptionType}.",
                    id, ex.GetType().Name);
            },
            cancellationToken);

        return new BillStatementProcessingBatchResult(
            candidateIds.Count, failures);
    }

    private async Task ProcessUploadAsync(
        BillStatementUploadEntity upload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            upload);

        if (upload.Status is not
            BillStatementUploadStatus.Uploaded and not
            BillStatementUploadStatus.Processing)
        {
            return;
        }

        if (upload.Status == BillStatementUploadStatus.Uploaded)
        {
            upload.Status = BillStatementUploadStatus.Processing;
            upload.UpdatedAtUtc = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        try
        {
            /*
             * Read/extract the document only once. The transient text is
             * passed into the structured extraction boundary and is not
             * persisted as raw text.
             */
            var extractionResult = _documentTextReader.Read(
                upload.UserId,
                upload.StorageKey,
                upload.MediaType,
                upload.FileExtension);

            if (extractionResult.RequiresOcr)
            {
                upload.Status = BillStatementUploadStatus.NeedsOcr;
                upload.UpdatedAtUtc = DateTimeOffset.UtcNow;

                await _dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            /*
             * Resolve Bill Stream context through the Bills-owned read
             * boundary. The context is a hint, not proof of statement facts.
             */
            var billStreamContext =
                await _billStreamGateway.GetOwnedAsync(
                    upload.UserId,
                    upload.BillStreamId,
                    cancellationToken);

            if (billStreamContext is null)
            {
                throw new InvalidOperationException(
                    "The statement upload is not associated with an owned Bill Stream.");
            }

            var structuredExtraction = await _extractionService.ExtractAsync(
                new BillStatementExtractionRequest(
                    DocumentText: extractionResult.Text,
                    Hints: new BillStatementExtractionHints(
                        ExpectedProviderName: billStreamContext.ProviderName,
                        ExpectedCategory: billStreamContext.Category.ToString())),
                cancellationToken);

            var parsedStatement = structuredExtraction.Statement;

            if (!structuredExtraction.IsReadyForValidation)
            {
                upload.Status = BillStatementUploadStatus.ReadyForParsing;
                upload.UpdatedAtUtc = DateTimeOffset.UtcNow;

                await _dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            var validationResult = _validationService.Validate(
                new BillStatementValidationInput(
                    TotalAmount: parsedStatement.TotalAmount,
                    PeriodStart: parsedStatement.BillingPeriodStart,
                    PeriodEnd: parsedStatement.BillingPeriodEnd,
                    StatementDate: parsedStatement.StatementDate,
                    DueDate: parsedStatement.DueDate,
                    CurrencyCode: parsedStatement.CurrencyCode),
                DateOnly.FromDateTime(DateTime.UtcNow));

            if (!validationResult.IsValid ||
                validationResult.RequiresReview)
            {
                upload.Status = BillStatementUploadStatus.ReadyForParsing;
                upload.UpdatedAtUtc = DateTimeOffset.UtcNow;

                await _dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            await _persistenceService.PersistAsync(
                upload,
                parsedStatement,
                structuredExtraction.LineItems,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
            when (ex is
                BillStatementTextExtractionException or
                IOException or
                UnauthorizedAccessException or
                InvalidOperationException or
                ArgumentException)
        {
            upload.Status = BillStatementUploadStatus.Failed;
            upload.UpdatedAtUtc = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Bill statement upload {UploadId} failed during document evidence processing with {ExceptionType}.",
                upload.Id,
                ex.GetType().Name);
        }
    }

}

public sealed class BillStatementProcessingBackgroundService
    : BackgroundService
{
    private const int ProcessingBatchSize = 4;

    private static readonly TimeSpan ErrorRetryDelay =
        TimeSpan.FromSeconds(1);

    private static readonly TimeSpan MaintenanceInterval =
        TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BillStatementProcessingSignal _signal;
    private readonly ILogger<BillStatementProcessingBackgroundService> _logger;
    private readonly Dictionary<Guid, DateTimeOffset> _deferredUploads = new();
    private static readonly TimeSpan PerUploadRetryDelay = TimeSpan.FromMinutes(5);
    private DateOnly? _lastSuccessfulDueReminderDate;
    private DateTimeOffset _nextDueReminderRetryUtc = DateTimeOffset.MinValue;

    public BillStatementProcessingBackgroundService(
        IServiceScopeFactory scopeFactory,
        BillStatementProcessingSignal signal,
        ILogger<BillStatementProcessingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _signal = signal;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileAccountDeletionQuarantineAsync(
                    stoppingToken);

                var processedCount = await ProcessBatchAsync(
                    stoppingToken);

                await RefreshDueRemindersWhenDueAsync(stoppingToken);

                if (processedCount > 0)
                {
                    continue;
                }

                await WaitForSignalOrMaintenanceAsync(
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    "The statement processing worker encountered {ExceptionType}.",
                    ex.GetType().Name);

                try
                {
                    await Task.Delay(
                        ErrorRetryDelay,
                        stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private async Task RefreshDueRemindersWhenDueAsync(
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        if (_lastSuccessfulDueReminderDate == today ||
            now < _nextDueReminderRetryUtc)
            return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var refresher = scope.ServiceProvider
                .GetRequiredService<BillStatementDueReminderRefreshService>();
            await refresher.RefreshAsync(today, now, cancellationToken);
            _lastSuccessfulDueReminderDate = today;
            _nextDueReminderRetryUtc = DateTimeOffset.MinValue;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // One reminder refresh failure should not stop queued uploads.
            // Avoid a hot retry loop while keeping the failure observable.
            _nextDueReminderRetryUtc = now.AddMinutes(10);
            _logger.LogWarning(
                "Daily statement reminder reconciliation failed with {ExceptionType}.",
                ex.GetType().Name);
        }
    }

    private async Task<int> ProcessBatchAsync(
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var processingService =
            scope.ServiceProvider.GetRequiredService<BillStatementProcessingService>();

        // Cooldown is in-memory only. After a restart, the upload is retried
        // once and then deferred again if it still fails unexpectedly.
        var now = DateTimeOffset.UtcNow;
        foreach (var id in _deferredUploads
            .Where(entry => entry.Value <= now)
            .Select(entry => entry.Key)
            .ToArray())
            _deferredUploads.Remove(id);

        var result = await processingService.ProcessPendingBatchDetailedAsync(
            ProcessingBatchSize,
            _deferredUploads.Keys.ToArray(),
            cancellationToken);

        foreach (var id in result.UnexpectedFailureIds)
            _deferredUploads[id] = now.Add(PerUploadRetryDelay);

        return result.ConsideredCount;
    }

    private async Task ReconcileAccountDeletionQuarantineAsync(
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var userExistenceGateway =
            scope.ServiceProvider.GetRequiredService<IIdentityUserExistenceGateway>();

        var statementStorage =
            scope.ServiceProvider.GetRequiredService<SecureBillStatementStorageService>();

        var recoveryLogger =
            scope.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger<AccountDeletionStatementQuarantineRecovery>();

        var recovery = new AccountDeletionStatementQuarantineRecovery(
            userExistenceGateway,
            statementStorage,
            recoveryLogger);

        await recovery.ReconcileAsync(cancellationToken);
    }

    private async Task WaitForSignalOrMaintenanceAsync(
        CancellationToken cancellationToken)
    {
        using var maintenanceCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        var signalTask =
            _signal.WaitAsync(
                    maintenanceCancellation.Token)
                .AsTask();

        var maintenanceTask =
            Task.Delay(
                MaintenanceInterval,
                maintenanceCancellation.Token);

        var completed = await Task.WhenAny(
            signalTask,
            maintenanceTask);

        maintenanceCancellation.Cancel();

        try
        {
            await completed;
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            // The losing wait was canceled after the other one completed.
        }
    }
}

/// <summary>One batch window; failures are deferred without changing financial evidence.</summary>
public sealed record BillStatementProcessingBatchResult(
    int ConsideredCount,
    IReadOnlyList<Guid> UnexpectedFailureIds);

/// <summary>Isolates each unexpected upload failure; cancellation is never swallowed.</summary>
public static class BillStatementProcessingBatchRunner
{
    public static async Task<IReadOnlyList<Guid>> RunAsync(
        IReadOnlyList<Guid> candidateIds,
        Func<Guid, CancellationToken, Task> process,
        Action<Guid, Exception> onUnexpectedFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidateIds);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(onUnexpectedFailure);

        var failedIds = new List<Guid>();
        foreach (var id in candidateIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await process(id, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failedIds.Add(id);
                onUnexpectedFailure(id, ex);
            }
        }

        return failedIds;
    }
}
