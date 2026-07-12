namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Connections;
using Microsoft.EntityFrameworkCore;

internal sealed class AdapterConnectionRepository(IngestionDbContext dbContext) : IAdapterConnectionRepository
{
    public Task<AdapterConnection?> GetAsync(Guid connectionId, CancellationToken cancellationToken) =>
        dbContext.AdapterConnections.FirstOrDefaultAsync(
            connection => connection.Id == connectionId,
            cancellationToken);

    public Task<AdapterConnection?> GetAsync(
        Guid propertyId,
        Guid connectionId,
        CancellationToken cancellationToken) => dbContext.AdapterConnections.FirstOrDefaultAsync(
        connection => connection.Id == connectionId && connection.PropertyId == propertyId,
        cancellationToken);

    public Task AddAsync(AdapterConnection connection, CancellationToken cancellationToken)
    {
        dbContext.AdapterConnections.Add(connection);
        return Task.CompletedTask;
    }
}
