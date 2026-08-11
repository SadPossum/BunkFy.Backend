namespace BunkFy.Modules.Inventory.Persistence.Repositories;

using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

internal sealed class BedRetirementRepository(InventoryDbContext dbContext) : IBedRetirementRepository
{
    private static readonly InventoryRetirementProcessState[] ActiveStates =
    [
        InventoryRetirementProcessState.Draining,
        InventoryRetirementProcessState.FinalizationRequested,
        InventoryRetirementProcessState.FinalizedAwaitingTopology,
        InventoryRetirementProcessState.Rejected
    ];

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

    public async Task<Guid?> GetTopologyChangeIdByBedAsync(
        Guid propertyId,
        Guid bedId,
        CancellationToken cancellationToken)
    {
        BedRetirementProcess? local = dbContext.BedRetirements.Local.SingleOrDefault(
            process =>
                process.BedId == bedId &&
                process.PropertyId == propertyId &&
                BedRetirementProcess.IsDrainActive(process.State));
        if (local is not null)
        {
            return local.Id;
        }

        Guid[] trackedIds = dbContext.BedRetirements.Local
            .Select(process => process.Id)
            .ToArray();
        return await dbContext.BedRetirements
            .AsNoTracking()
            .Where(process =>
                !trackedIds.Contains(process.Id) &&
                process.BedId == bedId &&
                process.PropertyId == propertyId &&
                ActiveStates.Contains(process.State))
            .Select(process => (Guid?)process.Id)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<BedRetirementProcess?> GetByBedAsync(
        Guid propertyId,
        Guid bedId,
        CancellationToken cancellationToken)
    {
        BedRetirementProcess? local = dbContext.BedRetirements.Local.SingleOrDefault(
            process =>
                process.BedId == bedId &&
                process.PropertyId == propertyId &&
                BedRetirementProcess.IsDrainActive(process.State));
        if (local is not null)
        {
            return local;
        }

        Guid[] trackedIds = dbContext.BedRetirements.Local
            .Select(process => process.Id)
            .ToArray();
        return await dbContext.BedRetirements.SingleOrDefaultAsync(
                process =>
                    !trackedIds.Contains(process.Id) &&
                    process.BedId == bedId &&
                    process.PropertyId == propertyId &&
                    ActiveStates.Contains(process.State),
                cancellationToken)
            .ConfigureAwait(false);
    }

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
        BedRetirementProcess[] local = dbContext.BedRetirements.Local
            .Where(process =>
                process.PropertyId == propertyId &&
                BedRetirementProcess.IsDrainActive(process.State) &&
                (roomIds.Contains(process.RoomId) || bedIds.Contains(process.BedId)))
            .ToArray();
        Guid[] trackedIds = dbContext.BedRetirements.Local
            .Select(process => process.Id)
            .ToArray();
        BedRetirementProcess[] persisted = await dbContext.BedRetirements
            .Where(process =>
                !trackedIds.Contains(process.Id) &&
                process.PropertyId == propertyId &&
                ActiveStates.Contains(process.State) &&
                (roomIds.Contains(process.RoomId) || bedIds.Contains(process.BedId)))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return persisted
            .Concat(local)
            .OrderBy(process => process.Id)
            .ToArray();
    }

    public Task ReloadAsync(
        BedRetirementProcess process,
        CancellationToken cancellationToken) =>
        dbContext.Entry(process).ReloadAsync(cancellationToken);

    public Task AddAsync(BedRetirementProcess process, CancellationToken cancellationToken)
    {
        dbContext.BedRetirements.Add(process);
        return Task.CompletedTask;
    }
}
