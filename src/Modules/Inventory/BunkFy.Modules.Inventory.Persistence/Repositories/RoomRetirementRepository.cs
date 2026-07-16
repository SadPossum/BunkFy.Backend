namespace BunkFy.Modules.Inventory.Persistence.Repositories;

using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

internal sealed class RoomRetirementRepository(InventoryDbContext dbContext) : IRoomRetirementRepository
{
    public Task<RoomRetirementProcess?> GetAsync(
        Guid propertyId,
        Guid topologyChangeId,
        CancellationToken cancellationToken) =>
        dbContext.RoomRetirements.Local.FirstOrDefault(
            process => process.Id == topologyChangeId && process.PropertyId == propertyId) is { } tracked
            ? Task.FromResult<RoomRetirementProcess?>(tracked)
            : dbContext.RoomRetirements.FirstOrDefaultAsync(
                process => process.Id == topologyChangeId && process.PropertyId == propertyId,
                cancellationToken);

    public Task<RoomRetirementProcess?> GetByRoomAsync(
        Guid propertyId,
        Guid roomId,
        CancellationToken cancellationToken) =>
        dbContext.RoomRetirements.Local.FirstOrDefault(
            process => process.RoomId == roomId && process.PropertyId == propertyId) is { } tracked
            ? Task.FromResult<RoomRetirementProcess?>(tracked)
            : dbContext.RoomRetirements.FirstOrDefaultAsync(
                process => process.RoomId == roomId && process.PropertyId == propertyId,
                cancellationToken);

    public async Task<IReadOnlyCollection<RoomRetirementProcess>> ListActiveForUnitsAsync(
        Guid propertyId,
        IReadOnlyCollection<Guid> inventoryUnitIds,
        CancellationToken cancellationToken)
    {
        Guid[] ids = inventoryUnitIds.Distinct().ToArray();
        Guid[] roomIds = await dbContext.InventoryUnits
            .AsNoTracking()
            .Where(unit => unit.PropertyId == propertyId && ids.Contains(unit.Id))
            .Select(unit => unit.RoomId)
            .Distinct()
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        InventoryRetirementProcessState[] activeStates =
        [
            InventoryRetirementProcessState.Draining,
            InventoryRetirementProcessState.FinalizationRequested,
            InventoryRetirementProcessState.FinalizedAwaitingTopology,
            InventoryRetirementProcessState.Rejected
        ];
        return await dbContext.RoomRetirements
            .Where(process =>
                process.PropertyId == propertyId &&
                roomIds.Contains(process.RoomId) &&
                activeStates.Contains(process.State))
            .OrderBy(process => process.Id)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task AddAsync(RoomRetirementProcess process, CancellationToken cancellationToken)
    {
        dbContext.RoomRetirements.Add(process);
        return Task.CompletedTask;
    }
}
