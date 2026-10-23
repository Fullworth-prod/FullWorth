using FullWorth.API.Services.Statements;

namespace FullWorth.Tests.Services;

public sealed class BillStatementProcessingBatchRunnerTests
{
    [Fact]
    public async Task UnexpectedFailure_DoesNotPreventLaterUploadsFromProcessing()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        var attempted = new List<Guid>();
        var logged = new List<Guid>();

        var failed = await BillStatementProcessingBatchRunner.RunAsync(
            [first, second, third],
            (id, _) =>
            {
                attempted.Add(id);
                if (id == first)
                    throw new InvalidOperationException("Synthetic failure; no statement data");
                return Task.CompletedTask;
            },
            (id, exception) =>
            {
                Assert.IsType<InvalidOperationException>(exception);
                logged.Add(id);
            });

        Assert.Equal(new[] { first, second, third }, attempted);
        Assert.Equal(first, Assert.Single(failed));
        Assert.Equal(first, Assert.Single(logged));
    }

    [Fact]
    public async Task Cancellation_StopsTheBatchWithoutMarkingTheUploadFailed()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var reachedSecond = false;
        var unexpected = new List<Guid>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            BillStatementProcessingBatchRunner.RunAsync(
                [first, second],
                (id, _) =>
                {
                    if (id == second)
                    {
                        reachedSecond = true;
                        return Task.CompletedTask;
                    }

                    cancellation.Cancel();
                    throw new OperationCanceledException(cancellation.Token);
                },
                (id, _) => unexpected.Add(id),
                cancellation.Token));

        Assert.False(reachedSecond);
        Assert.Empty(unexpected);
    }

    [Fact]
    public async Task EmptyBatch_IsSafeAndDoesNotInvokeFailureObserver()
    {
        var attempted = false;
        var failed = await BillStatementProcessingBatchRunner.RunAsync(
            [],
            (_, _) =>
            {
                attempted = true;
                return Task.CompletedTask;
            },
            (_, _) => throw new InvalidOperationException("Unexpected callback"));

        Assert.Empty(failed);
        Assert.False(attempted);
    }
}
