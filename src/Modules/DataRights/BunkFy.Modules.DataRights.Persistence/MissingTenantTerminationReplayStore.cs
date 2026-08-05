namespace BunkFy.Modules.DataRights.Persistence;

using BunkFy.Modules.DataRights.Application.Ports;

internal sealed class MissingTenantTerminationReplayStore
    : ITenantTerminationReplayStore
{
    internal const string FailureCode =
        "data-rights.tenant-termination-replay.provider-unavailable";
    private const string ProviderName = "unavailable";

    public Task<TenantTerminationReplayStoreReadiness> CheckReadinessAsync(
        CancellationToken cancellationToken) =>
        Task.FromResult(new TenantTerminationReplayStoreReadiness(
            ProviderName,
            IsReady: false,
            IsProductionGrade: false,
            FailureCode));

    public Task<TenantTerminationReplayAppendReceipt> AppendAsync(
        TenantTerminationReplayJournalEntry entry,
        CancellationToken cancellationToken) =>
        Task.FromException<TenantTerminationReplayAppendReceipt>(Unavailable());

    public Task<TenantTerminationReplayAttempt?> ReadAttemptAsync(
        TenantTerminationReplayAttemptCoordinate coordinate,
        CancellationToken cancellationToken) =>
        Task.FromException<TenantTerminationReplayAttempt?>(Unavailable());

    public Task<TenantTerminationReplayIntent?> ReadIntentAsync(
        string tenantId,
        Guid processId,
        CancellationToken cancellationToken) =>
        Task.FromException<TenantTerminationReplayIntent?>(Unavailable());

    public Task<TenantTerminationReplayCheckpoint>
        ReadTrustedCheckpointAsync(
            string tenantId,
            Guid processId,
            CancellationToken cancellationToken) =>
        Task.FromException<TenantTerminationReplayCheckpoint>(Unavailable());

    public Task<TenantTerminationReplayPage> ReadAfterAsync(
        string tenantId,
        Guid processId,
        TenantTerminationReplayCursor cursor,
        int pageSize,
        CancellationToken cancellationToken) =>
        Task.FromException<TenantTerminationReplayPage>(Unavailable());

    private static TenantTerminationReplayStoreException Unavailable() =>
        new(
            FailureCode,
            "The configured tenant-termination replay provider is unavailable.");
}
