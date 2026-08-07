namespace BunkFy.Modules.Properties.Application.Ports;

public interface IPropertiesCreationOperationLock
{
    Task AcquireAsync(
        string tenantId,
        Guid operationId,
        CancellationToken cancellationToken);
}
