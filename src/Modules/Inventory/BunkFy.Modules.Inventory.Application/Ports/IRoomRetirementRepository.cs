namespace BunkFy.Modules.Inventory.Application.Ports;

using BunkFy.Modules.Inventory.Domain.Aggregates;

public interface IRoomRetirementRepository
{
    Task<RoomRetirementProcess?> GetAsync(Guid propertyId, Guid topologyChangeId, CancellationToken cancellationToken);
    Task<RoomRetirementProcess?> GetByRoomAsync(Guid propertyId, Guid roomId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<RoomRetirementProcess>> ListActiveForUnitsAsync(
        Guid propertyId,
        IReadOnlyCollection<Guid> inventoryUnitIds,
        CancellationToken cancellationToken);
    Task AddAsync(RoomRetirementProcess process, CancellationToken cancellationToken);
}
