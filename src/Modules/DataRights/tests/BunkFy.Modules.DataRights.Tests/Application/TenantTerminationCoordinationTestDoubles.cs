namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;

internal sealed record TenantTerminationSignalCapture(
    string TenantId,
    Guid ProcessId,
    TenantTerminationProcessPhase Phase,
    TenantTerminationProcessStatus Status,
    long ProcessVersion,
    long OperationRevision,
    DateTimeOffset OccurredAtUtc);

internal sealed class RecordingTenantTerminationCoordinationSignal
    : ITenantTerminationCoordinationSignal
{
    public List<TenantTerminationSignalCapture> Captures { get; } = [];

    public Task<bool> EnqueueAsync(
        TenantTerminationProcess process,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        this.Captures.Add(new(
            process.ScopeId,
            process.Id,
            process.Phase,
            process.Status,
            process.Version,
            process.OperationRevision,
            occurredAtUtc));
        return Task.FromResult(true);
    }
}

internal sealed class StubTenantTerminationReplayStore
    : ITenantTerminationReplayStore
{
    private readonly Dictionary<
        TenantTerminationReplayAttemptCoordinate,
        TenantTerminationReplayAttempt> attempts = [];

    public void Add(TenantTerminationReplayAttempt attempt) =>
        this.attempts[attempt.Dispatch.Coordinate] = attempt;

    public Task<TenantTerminationReplayAttempt?> ReadAttemptAsync(
        TenantTerminationReplayAttemptCoordinate coordinate,
        CancellationToken cancellationToken) =>
        Task.FromResult(this.attempts.GetValueOrDefault(coordinate));

    public Task<TenantTerminationReplayIntent?> ReadIntentAsync(
        string tenantId,
        Guid processId,
        CancellationToken cancellationToken) =>
        Task.FromResult<TenantTerminationReplayIntent?>(null);

    public Task<TenantTerminationReplayStoreReadiness> CheckReadinessAsync(
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<TenantTerminationReplayAppendReceipt> AppendAsync(
        TenantTerminationReplayJournalEntry entry,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<TenantTerminationReplayCheckpoint> ReadTrustedCheckpointAsync(
        string tenantId,
        Guid processId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<TenantTerminationReplayPage> ReadAfterAsync(
        string tenantId,
        Guid processId,
        TenantTerminationReplayCursor cursor,
        int pageSize,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
