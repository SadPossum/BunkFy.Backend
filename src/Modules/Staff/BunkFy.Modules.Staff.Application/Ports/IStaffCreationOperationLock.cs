namespace BunkFy.Modules.Staff.Application.Ports;

public interface IStaffCreationOperationLock
{
    Task AcquireAsync(
        string tenantId,
        Guid operationId,
        CancellationToken cancellationToken);
}
