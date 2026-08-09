namespace BunkFy.Modules.Ingestion.Application.Ports;

internal interface IIngestionConnectionManagementOperationRepository
{
    Task<IngestionConnectionManagementOperationRecord?> GetAsync(
        Guid connectionId,
        Guid operationId,
        CancellationToken cancellationToken);

    Task AddAsync(
        IngestionConnectionManagementOperationRecord operation,
        CancellationToken cancellationToken);
}

internal sealed record IngestionConnectionManagementOperationRecord(
    Guid OperationId,
    string ScopeId,
    Guid PropertyId,
    Guid ConnectionId,
    IngestionConnectionManagementMutationKind Kind,
    long ExpectedVersion,
    string RequestFingerprint,
    long ResultVersion,
    DateTimeOffset CompletedAtUtc)
{
    public bool Matches(
        IngestionConnectionManagementMutationKind kind,
        Guid propertyId,
        Guid connectionId,
        long expectedVersion,
        string requestFingerprint) =>
        this.Kind == kind &&
        this.PropertyId == propertyId &&
        this.ConnectionId == connectionId &&
        this.ExpectedVersion == expectedVersion &&
        string.Equals(
            this.RequestFingerprint,
            requestFingerprint,
            StringComparison.Ordinal);
}

internal enum IngestionConnectionManagementMutationKind
{
    ConnectionCreate = 1,
    ConnectionUpdate = 2,
    ConnectionEnable = 3,
    ConnectionDisable = 4,
    PollingScheduleConfigure = 5,
    PollingScheduleClear = 6,
    CheckpointReset = 7
}
