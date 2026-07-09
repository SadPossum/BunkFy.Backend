namespace Ordering.Application.Ports;

public interface ICatalogItemProjectionRepository
{
    Task<CatalogItemProjectionSnapshot?> GetAsync(Guid catalogItemId, CancellationToken cancellationToken);
    Task UpsertAsync(CatalogItemProjectionWriteModel item, CancellationToken cancellationToken);
    Task MarkDiscontinuedAsync(string tenantId, Guid catalogItemId, CancellationToken cancellationToken);
}
