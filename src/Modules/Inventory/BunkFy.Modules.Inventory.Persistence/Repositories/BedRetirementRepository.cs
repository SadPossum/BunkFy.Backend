namespace BunkFy.Modules.Inventory.Persistence.Repositories;

using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

internal sealed class BedRetirementRepository(InventoryDbContext dbContext) : IBedRetirementRepository
{
    public Task<BedRetirementProcess?> GetAsync(
        Guid propertyId,
        Guid topologyChangeId,
        CancellationToken cancellationToken) =>
        dbContext.BedRetirements.Local.FirstOrDefault(
            process => process.Id == topologyChangeId && process.PropertyId == propertyId) is { } tracked
            ? Task.FromResult<BedRetirementProcess?>(tracked)
            : dbContext.BedRetirements.FirstOrDefaultAsync(
                process => process.Id == topologyChangeId && process.PropertyId == propertyId,
                cancellationToken);

    public Task<BedRetirementProcess?> GetByBedAsync(
        Guid propertyId,
        Guid bedId,
        CancellationToken cancellationToken) =>
        dbContext.BedRetirements.Local.FirstOrDefault(
            process => process.BedId == bedId && process.PropertyId == propertyId) is { } tracked
            ? Task.FromResult<BedRetirementProcess?>(tracked)
            : dbContext.BedRetirements.FirstOrDefaultAsync(
                process => process.BedId == bedId && process.PropertyId == propertyId,
                cancellationToken);

    public async Task<IReadOnlyCollection<BedRetirementProcess>> ListActiveForUnitsAsync(
        Guid propertyId,
        IReadOnlyCollection<Guid> inventoryUnitIds,
        CancellationToken cancellationToken)
    {
        Guid[] ids = inventoryUnitIds.Distinct().ToArray();
        var units = await dbContext.InventoryUnits
            .AsNoTracking()
            .Where(unit => unit.PropertyId == propertyId && ids.Contains(unit.Id))
            .Select(unit => new { unit.Id, unit.RoomId, unit.Kind })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        Guid[] roomIds = units
            .Where(unit => unit.Kind == InventoryUnitKind.Room)
            .Select(unit => unit.RoomId)
            .Distinct()
            .ToArray();
        Guid[] bedIds = units
            .Where(unit => unit.Kind == InventoryUnitKind.Bed)
            .Select(unit => unit.Id)
            .Distinct()
            .ToArray();
        InventoryRetirementProcessState[] activeStates =
        [
            InventoryRetirementProcessState.Draining,
            InventoryRetirementProcessState.FinalizationRequested,
            InventoryRetirementProcessState.FinalizedAwaitingTopology,
            InventoryRetirementProcessState.Rejected
        ];
        return await dbContext.BedRetirements
            .Where(process =>
                process.PropertyId == propertyId &&
                activeStates.Contains(process.State) &&
                (roomIds.Contains(process.RoomId) || bedIds.Contains(process.BedId)))
            .OrderBy(process => process.Id)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task AddAsync(BedRetirementProcess process, CancellationToken cancellationToken)
    {
        dbContext.BedRetirements.Add(process);
        return Task.CompletedTask;
    }
}
