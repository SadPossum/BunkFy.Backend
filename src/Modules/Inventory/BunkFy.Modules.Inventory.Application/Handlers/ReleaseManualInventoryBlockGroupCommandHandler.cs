namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class ReleaseManualInventoryBlockGroupCommandHandler(
    IManualInventoryBlockRepository blocks,
    IInventoryAvailabilityRepository availability,
    InventoryRetirementCoordinator retirements,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<ReleaseManualInventoryBlockGroupCommand, ManualInventoryBlockGroupDto>
{
    public async Task<Result<ManualInventoryBlockGroupDto>> HandleAsync(
        ReleaseManualInventoryBlockGroupCommand command,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<ManualInventoryBlock> group = await blocks
            .GetActiveGroupAsync(command.PropertyId, command.BlockGroupId, cancellationToken)
            .ConfigureAwait(false);
        if (group.Count == 0)
        {
            return Result.Failure<ManualInventoryBlockGroupDto>(InventoryApplicationErrors.BlockGroupNotFound);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        foreach (ManualInventoryBlock block in group)
        {
            Result released = block.Release(block.Version, idGenerator.NewId(), nowUtc, command.ActorId);
            if (released.IsFailure)
            {
                return Result.Failure<ManualInventoryBlockGroupDto>(released.Error);
            }
        }

        Guid[] inventoryUnitIds = group.Select(block => block.InventoryUnitId).Distinct().ToArray();
        await availability.TouchUnitsAsync(
            command.PropertyId,
            inventoryUnitIds,
            cancellationToken).ConfigureAwait(false);
        await retirements.TryAdvanceForUnitsAsync(
            command.PropertyId,
            inventoryUnitIds,
            excludedAllocationId: null,
            excludedBlockIds: group.Select(block => block.Id).ToArray(),
            cancellationToken).ConfigureAwait(false);
        return Result.Success(group.ToGroupDto(command.BlockGroupId));
    }
}
