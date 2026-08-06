namespace BunkFy.Modules.Retention.Application.Ports;

internal interface IRetentionMutationLock
{
    Task AcquireTenantTargetReadAsync(
        string tenantId,
        CancellationToken cancellationToken);

    Task AcquireTenantTargetWriteAsync(
        string tenantId,
        CancellationToken cancellationToken);

    Task AcquirePropertyTargetReadAsync(
        string tenantId,
        Guid propertyId,
        CancellationToken cancellationToken);

    Task AcquirePropertyTargetWriteAsync(
        string tenantId,
        Guid propertyId,
        CancellationToken cancellationToken);

    Task AcquireExecutionAsync(
        string tenantId,
        Guid executionId,
        CancellationToken cancellationToken);

    Task AcquireScheduleAsync(
        string tenantId,
        string ownerKey,
        string dataClassKey,
        Guid? propertyId,
        int executionPolicyVersion,
        CancellationToken cancellationToken);
}
