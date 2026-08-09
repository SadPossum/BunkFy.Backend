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
    InventoryManagementMutationCoordinator mutations,
    InventoryManagementOperationJournal journal,
    IManualInventoryBlockRepository blocks,
    IInventoryAvailabilityRepository availability,
    InventoryRetirementCoordinator retirements,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<ReleaseManualInventoryBlockGroupCommand, ManualInventoryBlockGroupMutationReceiptDto>
{
    public async Task<Result<ManualInventoryBlockGroupMutationReceiptDto>> HandleAsync(
        ReleaseManualInventoryBlockGroupCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OperationId == Guid.Empty)
        {
            return Result.Failure<
                ManualInventoryBlockGroupMutationReceiptDto>(
                InventoryApplicationErrors.ManagementOperationInvalid);
        }

        string fingerprint = InventoryManagementMutationFingerprint
            .ComputeManualBlockGroupRelease(
                command.PropertyId,
                command.BlockGroupId);
        await mutations.AcquireBlockGroupAsync(
                command.BlockGroupId,
                cancellationToken)
            .ConfigureAwait(false);
        InventoryManagementReplayDecision<
            ManualInventoryBlockGroupMutationReceiptDto> replay =
            await journal.InspectBlockGroupReleaseAsync(
                command.PropertyId,
                command.BlockGroupId,
                command.OperationId,
                fingerprint,
                cancellationToken).ConfigureAwait(false);
        if (replay.Exists)
        {
            return replay.ToResult();
        }

        IReadOnlyCollection<ManualInventoryBlock> group = await blocks
            .GetActiveGroupAsync(command.PropertyId, command.BlockGroupId, cancellationToken)
            .ConfigureAwait(false);
        if (group.Count == 0)
        {
            return Result.Failure<ManualInventoryBlockGroupMutationReceiptDto>(InventoryApplicationErrors.BlockGroupNotFound);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        foreach (ManualInventoryBlock block in group)
        {
            Result released = block.Release(block.Version, idGenerator.NewId(), nowUtc, command.ActorId);
            if (released.IsFailure)
            {
                return Result.Failure<ManualInventoryBlockGroupMutationReceiptDto>(released.Error);
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
        ManualInventoryBlockGroupMutationReceiptDto receipt = new(
            command.BlockGroupId,
            command.PropertyId,
            group.Count);
        receipt = await journal.RecordBlockGroupReleaseAsync(
            group.First().ScopeId,
            receipt,
            command.OperationId,
            fingerprint,
            nowUtc,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(receipt);
    }
}
