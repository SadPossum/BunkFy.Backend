namespace BunkFy.Modules.Inventory.Persistence.Repositories;

using Gma.Framework.Pagination;
using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

internal sealed class ManualInventoryBlockRepository(InventoryDbContext dbContext)
    : IManualInventoryBlockRepository
{
    public Task AddAsync(ManualInventoryBlock block, CancellationToken cancellationToken)
    {
        dbContext.ManualBlocks.Add(block);
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(IReadOnlyCollection<ManualInventoryBlock> blocks, CancellationToken cancellationToken)
    {
        dbContext.ManualBlocks.AddRange(blocks);
        return Task.CompletedTask;
    }

    public Task<ManualInventoryBlock?> GetAsync(
        Guid propertyId,
        Guid blockId,
        CancellationToken cancellationToken) =>
        dbContext.ManualBlocks.FirstOrDefaultAsync(
            block => block.Id == blockId && block.PropertyId == propertyId,
            cancellationToken);

    public async Task<IReadOnlyCollection<ManualInventoryBlock>> GetActiveGroupAsync(
        Guid propertyId,
        Guid blockGroupId,
        CancellationToken cancellationToken) =>
        await dbContext.ManualBlocks
            .Where(block =>
                block.PropertyId == propertyId &&
                block.BlockGroupId == blockGroupId &&
                block.Status == ManualInventoryBlockState.Active)
            .OrderBy(block => block.InventoryUnitId)
            .ThenBy(block => block.Id)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<bool> HasActiveOverlapAsync(
        Guid inventoryUnitId,
        DateOnly arrival,
        DateOnly departure,
        CancellationToken cancellationToken) =>
        dbContext.ManualBlocks.AnyAsync(
            block => block.InventoryUnitId == inventoryUnitId &&
                     block.Status == ManualInventoryBlockState.Active &&
                     block.Arrival < departure &&
                     arrival < block.Departure,
            cancellationToken);

    public Task<bool> HasAnyActiveOverlapAsync(
        IReadOnlyCollection<Guid> inventoryUnitIds,
        DateOnly arrival,
        DateOnly departure,
        CancellationToken cancellationToken) =>
        dbContext.ManualBlocks.AnyAsync(
            block => inventoryUnitIds.Contains(block.InventoryUnitId) &&
                     block.Status == ManualInventoryBlockState.Active &&
                     block.Arrival < departure &&
                     arrival < block.Departure,
            cancellationToken);

    public async Task TouchUnitAsync(Guid inventoryUnitId, CancellationToken cancellationToken)
    {
        InventoryUnit unit = await dbContext.InventoryUnits
            .SingleAsync(item => item.Id == inventoryUnitId, cancellationToken)
            .ConfigureAwait(false);
        unit.TouchAvailability();
    }

    public async Task TouchUnitsAsync(
        IReadOnlyCollection<Guid> inventoryUnitIds,
        CancellationToken cancellationToken)
    {
        InventoryUnit[] units = await dbContext.InventoryUnits
            .Where(unit => inventoryUnitIds.Contains(unit.Id))
            .OrderBy(unit => unit.Id)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (InventoryUnit unit in units)
        {
            unit.TouchAvailability();
        }
    }

    public async Task<ManualInventoryBlockListResponse> ListAsync(
        Guid propertyId,
        Guid? inventoryUnitId,
        bool includeReleased,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        IQueryable<ManualInventoryBlock> query = dbContext.ManualBlocks
            .AsNoTracking()
            .Where(block => block.PropertyId == propertyId);
        if (inventoryUnitId.HasValue)
        {
            query = query.Where(block => block.InventoryUnitId == inventoryUnitId.Value);
        }

        if (!includeReleased)
        {
            query = query.Where(block => block.Status == ManualInventoryBlockState.Active);
        }

        ManualInventoryBlockDto[] blocks = await query
            .OrderBy(block => block.Arrival)
            .ThenBy(block => block.InventoryUnitId)
            .ThenBy(block => block.Id)
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize)
            .Select(block => new ManualInventoryBlockDto(
                block.Id,
                block.BlockGroupId,
                block.PropertyId,
                block.InventoryUnitId,
                block.Arrival,
                block.Departure,
                block.Reason,
                block.Status == ManualInventoryBlockState.Active
                    ? ManualInventoryBlockStatus.Active
                    : ManualInventoryBlockStatus.Released,
                block.Version,
                block.CreatedAtUtc,
                block.ReleasedAtUtc))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return new(blocks, pageRequest.Page, pageRequest.PageSize);
    }
}
