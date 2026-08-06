namespace BunkFy.Modules.DataRights.Application.Ports;

public interface IDataRightsOperationLock
{
    Task AcquireTenantControlAsync(CancellationToken cancellationToken);

    Task AcquireProcessReadAsync(
        Guid processId,
        CancellationToken cancellationToken);

    Task AcquireProcessWriteAsync(
        Guid processId,
        CancellationToken cancellationToken);

    Task AcquireOwnerWorkItemAsync(
        Guid workItemId,
        CancellationToken cancellationToken);

    Task AcquireCaseReadAsync(
        Guid caseId,
        CancellationToken cancellationToken);

    Task AcquireCaseWriteAsync(
        Guid caseId,
        CancellationToken cancellationToken);

    Task AcquireExecutionWorkItemAsync(
        Guid workItemId,
        CancellationToken cancellationToken);

    Task AcquireProcessingLedgerAsync(CancellationToken cancellationToken);
}
