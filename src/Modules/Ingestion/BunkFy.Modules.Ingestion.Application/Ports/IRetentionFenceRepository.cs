namespace BunkFy.Modules.Ingestion.Application.Ports;

public interface IRetentionFenceRepository
{
    Task<bool> TryAdvanceAsync(Guid propertyId, CancellationToken cancellationToken);
}
