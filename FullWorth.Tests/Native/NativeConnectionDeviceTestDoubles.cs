using System.Threading.Channels;
using System.Windows.Input;

namespace FullWorth.ViewModels
{
    // Test compilation only. Production still resolves MAUI Command/Launcher;
    // no browser, device storage or OS application is opened by these tests.
    public sealed class Command(Action execute, Func<bool> canExecute) : ICommand
    {
        public Command(Action execute) : this(execute, () => true) { }
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => canExecute();
        public void Execute(object? parameter) => execute();
        public void ChangeCanExecute() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    // Test compilation only; financial dashboard tests never navigate an OS shell.
    public static class Shell
    {
        public static TestShell Current { get; } = new();
        public sealed class TestShell
        {
            public Task GoToAsync(string route) => Task.CompletedTask;
        }
    }

    public static class Launcher
    {
        public static TestLauncher Default { get; } = new();
        public sealed class TestLauncher
        {
            public Task<bool> OpenAsync(string url) => Task.FromResult(true);
        }
    }
}

namespace FullWorth.Tests.Native
{
    internal sealed class NativeStepTimeProvider : TimeProvider
    {
        private readonly Channel<StepTimer> _scheduled = Channel.CreateUnbounded<StepTimer>();
        private long _ticks;
        public TimeSpan UtcAdjustment { get; set; }
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref _ticks);
        public override DateTimeOffset GetUtcNow() =>
            new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero).AddTicks(GetTimestamp()).Add(UtcAdjustment);

        public override ITimer CreateTimer(TimerCallback callback, object? state,
            TimeSpan dueTime, TimeSpan period)
        {
            if (period != Timeout.InfiniteTimeSpan) throw new InvalidOperationException("One-shot delay fixture only.");
            var timer = new StepTimer(callback, state, GetTimestamp() + dueTime.Ticks);
            _scheduled.Writer.TryWrite(timer);
            return timer;
        }

        internal Task WaitForScheduledAsync() => _scheduled.Reader.WaitToReadAsync().AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5));

        internal async Task StepAsync(Task operation)
        {
            while (!operation.IsCompleted)
            {
                using var read = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var next = _scheduled.Reader.ReadAsync(read.Token).AsTask();
                if (await Task.WhenAny(next, operation) == operation)
                {
                    read.Cancel();
                    try { await next; } catch (OperationCanceledException) { }
                    return;
                }
                var timer = await next;
                if (timer.Disposed) continue;
                Interlocked.Exchange(ref _ticks, Math.Max(GetTimestamp(), timer.Due));
                timer.Fire();
                return;
            }
        }

        private sealed class StepTimer(TimerCallback callback, object? state, long due) : ITimer
        {
            public long Due { get; } = due;
            private int _disposed;
            public bool Disposed => Volatile.Read(ref _disposed) != 0;
            public void Fire() { if (!Disposed) callback(state); }
            public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();
            public void Dispose() => Interlocked.Exchange(ref _disposed, 1);
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
