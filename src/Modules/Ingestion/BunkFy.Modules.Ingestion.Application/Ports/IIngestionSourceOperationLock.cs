namespace BunkFy.Modules.Ingestion.Application.Ports;

internal interface IIngestionSourceOperationLock
{
    Task AcquireAsync(
        string tenantId,
        Guid sourceLinkId,
        CancellationToken cancellationToken);
}
