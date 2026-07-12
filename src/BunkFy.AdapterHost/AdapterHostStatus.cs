namespace BunkFy.AdapterHost;

using BunkFy.Adapter.Abstractions;

public enum AdapterHostCycleState
{
    Starting = 0,
    Idle = 1,
    Running = 2,
    Delaying = 3,
    Stopped = 4
}

public sealed record AdapterHostStatusSnapshot(
    string AdapterType,
    Guid ConnectionId,
    AdapterHostCycleState State,
    bool Ready,
    bool HasCheckpoint,
    DateTimeOffset? LastCycleStartedAtUtc,
    DateTimeOffset? LastCycleCompletedAtUtc,
    AdapterRunOutcome? LastOutcome,
    string? LastErrorCode,
    DateTimeOffset? NextCycleAtUtc,
    int ConsecutiveFailures);

public sealed class AdapterHostStatus(AdapterHostOptions options)
{
    private readonly Lock gate = new();
    private AdapterHostCycleState state = AdapterHostCycleState.Starting;
    private bool ready;
    private bool hasCheckpoint;
    private DateTimeOffset? lastStarted;
    private DateTimeOffset? lastCompleted;
    private AdapterRunOutcome? lastOutcome;
    private string? lastErrorCode;
    private DateTimeOffset? nextCycle;
    private int consecutiveFailures;

    public AdapterHostStatusSnapshot Snapshot()
    {
        lock (this.gate)
        {
            return new(
                options.AdapterType,
                options.ConnectionId,
                this.state,
                this.ready,
                this.hasCheckpoint,
                this.lastStarted,
                this.lastCompleted,
                this.lastOutcome,
                this.lastErrorCode,
                this.nextCycle,
                this.consecutiveFailures);
        }
    }

    internal void SetReady(bool hasCheckpoint, DateTimeOffset? nextCycleAtUtc)
    {
        lock (this.gate)
        {
            this.ready = true;
            this.hasCheckpoint = hasCheckpoint;
            this.state = AdapterHostCycleState.Idle;
            this.nextCycle = nextCycleAtUtc;
        }
    }

    internal void SetRunning(DateTimeOffset startedAtUtc)
    {
        lock (this.gate)
        {
            this.state = AdapterHostCycleState.Running;
            this.lastStarted = startedAtUtc;
            this.nextCycle = null;
        }
    }

    internal void SetCompleted(
        DateTimeOffset completedAtUtc,
        AdapterRunOutcome outcome,
        string? errorCode,
        bool hasCheckpoint,
        bool failed,
        DateTimeOffset nextCycleAtUtc)
    {
        lock (this.gate)
        {
            this.state = AdapterHostCycleState.Delaying;
            this.lastCompleted = completedAtUtc;
            this.lastOutcome = outcome;
            this.lastErrorCode = errorCode;
            this.hasCheckpoint = hasCheckpoint;
            this.consecutiveFailures = failed ? checked(this.consecutiveFailures + 1) : 0;
            this.nextCycle = nextCycleAtUtc;
        }
    }

    internal void SetException(
        DateTimeOffset completedAtUtc,
        bool hasCheckpoint,
        DateTimeOffset nextCycleAtUtc)
    {
        lock (this.gate)
        {
            this.state = AdapterHostCycleState.Delaying;
            this.lastCompleted = completedAtUtc;
            this.lastOutcome = AdapterRunOutcome.Failed;
            this.lastErrorCode = "adapter.runtime-cycle-failed";
            this.hasCheckpoint = hasCheckpoint;
            this.consecutiveFailures = checked(this.consecutiveFailures + 1);
            this.nextCycle = nextCycleAtUtc;
        }
    }

    internal void SetLeaseUnavailable(DateTimeOffset completedAtUtc, DateTimeOffset nextCycleAtUtc)
    {
        lock (this.gate)
        {
            this.state = AdapterHostCycleState.Delaying;
            this.lastCompleted = completedAtUtc;
            this.lastOutcome = null;
            this.lastErrorCode = "adapter.remote-lease-unavailable";
            this.consecutiveFailures = 0;
            this.nextCycle = nextCycleAtUtc;
        }
    }

    internal void SetStopped()
    {
        lock (this.gate)
        {
            this.ready = false;
            this.state = AdapterHostCycleState.Stopped;
            this.nextCycle = null;
        }
    }
}
