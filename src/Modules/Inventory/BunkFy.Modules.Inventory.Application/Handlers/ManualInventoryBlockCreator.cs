namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class ManualInventoryBlockCreator(
    IInventoryReadRepository inventory,
    IManualInventoryBlockRepository blocks,
    IInventoryAvailabilityRepository availability,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator idGenerator)
{
    public async Task<Result<ManualInventoryBlockCreationResult>> CreateAsync(
        Guid propertyId,
        InventoryBlockTarget target,
        DateOnly arrival,
        DateOnly departure,
        string reason,
        string? actorId,
        CancellationToken cancellationToken)
    {
        string? scopeId = scopeContext.ScopeId;
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeId))
        {
            return Result.Failure<ManualInventoryBlockCreationResult>(InventoryApplicationErrors.TenantRequired);
        }

        if (!InventoryBlockTargetNormalizer.TryNormalize(
                target,
                out InventoryBlockTarget normalizedTarget))
        {
            return Result.Failure<ManualInventoryBlockCreationResult>(
                InventoryApplicationErrors.BlockTargetInvalid);
        }

        string normalizedReason =
            InventoryManagementMutationFingerprint.NormalizeReason(reason);

        if (!await inventory.PropertyExistsAsync(propertyId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ManualInventoryBlockCreationResult>(InventoryApplicationErrors.PropertyNotFound);
        }

        IReadOnlyCollection<InventoryUnitSnapshot> resolved = await inventory
            .ResolveBlockTargetUnitsAsync(
                propertyId,
                normalizedTarget,
                cancellationToken)
            .ConfigureAwait(false);
        if (normalizedTarget.Kind == InventoryBlockTargetKind.Unit)
        {
            InventoryUnitSnapshot? unit = resolved.SingleOrDefault();
            if (unit is null)
            {
                return Result.Failure<ManualInventoryBlockCreationResult>(InventoryApplicationErrors.InventoryUnitNotFound);
            }

            if (!unit.Unit.IsTopologyActive)
            {
                return Result.Failure<ManualInventoryBlockCreationResult>(InventoryApplicationErrors.InventoryUnitInactive);
            }

            if (!unit.IsSellable)
            {
                return Result.Failure<ManualInventoryBlockCreationResult>(InventoryApplicationErrors.InventoryUnitNotSellable);
            }
        }

        Guid[] inventoryUnitIds = resolved
            .Where(unit => unit.IsSellable)
            .Select(unit => unit.Unit.InventoryUnitId)
            .Distinct()
            .OrderBy(inventoryUnitId => inventoryUnitId)
            .ToArray();
        if (inventoryUnitIds.Length == 0)
        {
            return Result.Failure<ManualInventoryBlockCreationResult>(InventoryApplicationErrors.BlockTargetEmpty);
        }

        InventoryAvailabilityContextSnapshot context = await availability
            .GetContextAsync(propertyId, inventoryUnitIds, cancellationToken)
            .ConfigureAwait(false);
        InventoryAvailabilityConflictSnapshot conflicts = await availability.GetConflictsAsync(
                propertyId,
                context.ConflictUnitIds,
                arrival,
                departure,
                excludedAllocationId: null,
                excludedBlockIds: [],
                cancellationToken).ConfigureAwait(false);
        if (conflicts.HasManualBlockConflict)
        {
            return Result.Failure<ManualInventoryBlockCreationResult>(InventoryApplicationErrors.BlockOverlap);
        }

        if (conflicts.HasActiveAllocationConflict)
        {
            return Result.Failure<ManualInventoryBlockCreationResult>(InventoryApplicationErrors.BlockAllocationConflict);
        }

        Guid blockGroupId = idGenerator.NewId();
        DateTimeOffset nowUtc = clock.UtcNow;
        List<ManualInventoryBlock> created = new(inventoryUnitIds.Length);
        foreach (Guid inventoryUnitId in inventoryUnitIds)
        {
            Result<ManualInventoryBlock> result = ManualInventoryBlock.Create(
                idGenerator.NewId(),
                blockGroupId,
                scopeId,
                propertyId,
                inventoryUnitId,
                arrival,
                departure,
                normalizedReason,
                idGenerator.NewId(),
                nowUtc,
                actorId);
            if (result.IsFailure)
            {
                return Result.Failure<ManualInventoryBlockCreationResult>(result.Error);
            }

            created.Add(result.Value);
        }

        await availability.TouchUnitsAsync(
            propertyId,
            inventoryUnitIds,
            cancellationToken).ConfigureAwait(false);
        await blocks.AddRangeAsync(created, cancellationToken).ConfigureAwait(false);
        return Result.Success(new ManualInventoryBlockCreationResult(blockGroupId, created));
    }
}

internal sealed record ManualInventoryBlockCreationResult(
    Guid BlockGroupId,
    IReadOnlyCollection<ManualInventoryBlock> Blocks);
