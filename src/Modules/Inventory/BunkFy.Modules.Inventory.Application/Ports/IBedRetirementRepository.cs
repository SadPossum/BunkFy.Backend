namespace BunkFy.Modules.Inventory.Application.Ports;

using BunkFy.Modules.Inventory.Domain.Aggregates;

public interface IBedRetirementRepository
{
    Task<BedRetirementProcess?> GetAsync(Guid propertyId, Guid topologyChangeId, CancellationToken cancellationToken);
    Task<Guid?> GetTopologyChangeIdByBedAsync(
        Guid propertyId,
        Guid bedId,
        CancellationToken cancellationToken);
    Task<BedRetirementProcess?> GetByBedAsync(Guid propertyId, Guid bedId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<BedRetirementProcess>> ListActiveForUnitsAsync(
        Guid propertyId,
        IReadOnlyCollection<Guid> inventoryUnitIds,
        CancellationToken cancellationToken);
    Task ReloadAsync(BedRetirementProcess process, CancellationToken cancellationToken);
    Task AddAsync(BedRetirementProcess process, CancellationToken cancellationToken);
}
