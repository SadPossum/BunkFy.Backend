namespace BunkFy.Modules.Inventory.Persistence.Repositories;

using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

internal sealed class RoomRetirementRepository(InventoryDbContext dbContext) : IRoomRetirementRepository
{
    private static readonly InventoryRetirementProcessState[] ActiveStates =
    [
        InventoryRetirementProcessState.Draining,
        InventoryRetirementProcessState.FinalizationRequested,
        InventoryRetirementProcessState.FinalizedAwaitingTopology,
        InventoryRetirementProcessState.Rejected
    ];

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

    public async Task<Guid?> GetTopologyChangeIdByRoomAsync(
        Guid propertyId,
        Guid roomId,
        CancellationToken cancellationToken)
    {
        RoomRetirementProcess? local = dbContext.RoomRetirements.Local.SingleOrDefault(
            process =>
                process.RoomId == roomId &&
                process.PropertyId == propertyId &&
                RoomRetirementProcess.IsDrainActive(process.State));
        if (local is not null)
        {
            return local.Id;
        }

        Guid[] trackedIds = dbContext.RoomRetirements.Local
            .Select(process => process.Id)
            .ToArray();
        return await dbContext.RoomRetirements
            .AsNoTracking()
            .Where(process =>
                !trackedIds.Contains(process.Id) &&
                process.RoomId == roomId &&
                process.PropertyId == propertyId &&
                ActiveStates.Contains(process.State))
            .Select(process => (Guid?)process.Id)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<RoomRetirementProcess?> GetByRoomAsync(
        Guid propertyId,
        Guid roomId,
        CancellationToken cancellationToken)
    {
        RoomRetirementProcess? local = dbContext.RoomRetirements.Local.SingleOrDefault(
            process =>
                process.RoomId == roomId &&
                process.PropertyId == propertyId &&
                RoomRetirementProcess.IsDrainActive(process.State));
        if (local is not null)
        {
            return local;
        }

        Guid[] trackedIds = dbContext.RoomRetirements.Local
            .Select(process => process.Id)
            .ToArray();
        return await dbContext.RoomRetirements.SingleOrDefaultAsync(
                process =>
                    !trackedIds.Contains(process.Id) &&
                    process.RoomId == roomId &&
                    process.PropertyId == propertyId &&
                    ActiveStates.Contains(process.State),
                cancellationToken)
            .ConfigureAwait(false);
    }

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
        RoomRetirementProcess[] local = dbContext.RoomRetirements.Local
            .Where(process =>
                process.PropertyId == propertyId &&
                roomIds.Contains(process.RoomId) &&
                RoomRetirementProcess.IsDrainActive(process.State))
            .ToArray();
        Guid[] trackedIds = dbContext.RoomRetirements.Local
            .Select(process => process.Id)
            .ToArray();
        RoomRetirementProcess[] persisted = await dbContext.RoomRetirements
            .Where(process =>
                !trackedIds.Contains(process.Id) &&
                process.PropertyId == propertyId &&
                roomIds.Contains(process.RoomId) &&
                ActiveStates.Contains(process.State))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return persisted
            .Concat(local)
            .OrderBy(process => process.Id)
            .ToArray();
    }

    public Task ReloadAsync(
        RoomRetirementProcess process,
        CancellationToken cancellationToken) =>
        dbContext.Entry(process).ReloadAsync(cancellationToken);

    public Task AddAsync(RoomRetirementProcess process, CancellationToken cancellationToken)
    {
        dbContext.RoomRetirements.Add(process);
        return Task.CompletedTask;
    }
}
