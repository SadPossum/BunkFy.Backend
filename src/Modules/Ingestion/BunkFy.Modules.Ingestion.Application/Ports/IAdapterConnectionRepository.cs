namespace BunkFy.Modules.Ingestion.Application.Ports;

using BunkFy.Modules.Ingestion.Domain.Connections;

public interface IAdapterConnectionRepository
{
    Task<AdapterConnection?> GetAsync(Guid connectionId, CancellationToken cancellationToken);
    Task<AdapterConnection?> GetAsync(Guid propertyId, Guid connectionId, CancellationToken cancellationToken);
    Task AddAsync(AdapterConnection connection, CancellationToken cancellationToken);
}
