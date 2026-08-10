namespace Integration.Tests.Support;

using Gma.Modules.Notifications.Application;
using Gma.Modules.Notifications.Application.Ports;

internal sealed class ControllableNotificationStreamClock(DateTimeOffset initialUtc) : TimeProvider
{
    private long utcTicks = initialUtc.UtcTicks;

    public override DateTimeOffset GetUtcNow() =>
        new(Interlocked.Read(ref this.utcTicks), TimeSpan.Zero);

    public void Advance(TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        Interlocked.Add(ref this.utcTicks, duration.Ticks);
    }
}

internal sealed class ControllableNotificationStreamPulse : INotificationStreamPulse
{
    private readonly StreamState history = new();
    private readonly StreamState broadcasts = new();

    public long CaptureVersion(NotificationStreamKind streamKind) =>
        this.State(streamKind).CaptureVersion();

    public ValueTask<bool> WaitForChangeAsync(
        NotificationStreamKind streamKind,
        long observedVersion,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        this.State(streamKind).WaitForChangeAsync(observedVersion, timeout, cancellationToken);

    public void Pulse(NotificationStreamKind streamKind) =>
        this.State(streamKind).Pulse();

    private StreamState State(NotificationStreamKind streamKind) => streamKind switch
    {
        NotificationStreamKind.History => this.history,
        NotificationStreamKind.Broadcasts => this.broadcasts,
        _ => throw new ArgumentOutOfRangeException(nameof(streamKind), streamKind, "Notification stream kind is invalid.")
    };

    private sealed class StreamState
    {
        private readonly Lock sync = new();
        private long version;
        private TaskCompletionSource change = NewSignal();

        public long CaptureVersion() => Interlocked.Read(ref this.version);

        public void Pulse()
        {
            TaskCompletionSource completedSignal;
            lock (this.sync)
            {
                Interlocked.Increment(ref this.version);
                completedSignal = this.change;
                this.change = NewSignal();
            }

            completedSignal.TrySetResult();
        }

        public async ValueTask<bool> WaitForChangeAsync(
            long observedVersion,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            Task signal;
            lock (this.sync)
            {
                if (this.version != observedVersion)
                {
                    return true;
                }

                signal = this.change.Task;
            }

            try
            {
                await signal.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (TimeoutException)
            {
                return false;
            }
        }

        private static TaskCompletionSource NewSignal() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
