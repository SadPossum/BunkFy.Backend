namespace Inventory.Application.Ports;

public interface IInventoryTopologyRepository
{
    Task ApplyPropertyAsync(InventoryPropertyTopologyWriteModel property, CancellationToken cancellationToken);
    Task ApplyRoomAsync(InventoryRoomTopologyWriteModel room, CancellationToken cancellationToken);
    Task ApplyBedAsync(InventoryBedTopologyWriteModel bed, CancellationToken cancellationToken);
    Task<InventoryRoomTopologySnapshot?> GetRoomAsync(Guid propertyId, Guid roomId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<InventoryUnitDefinitionSnapshot>> GetUnitDefinitionsAsync(
        Guid propertyId,
        Guid? roomId,
        Guid? inventoryUnitId,
        bool touchVersions,
        CancellationToken cancellationToken);
}
