namespace BunkFy.Modules.Inventory.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;

internal sealed class ReleaseManualInventoryBlockCommandHandler(
    InventoryManagementMutationCoordinator mutations,
    InventoryManagementOperationJournal journal,
    IManualInventoryBlockRepository blocks,
    IInventoryAvailabilityRepository availability,
    InventoryRetirementCoordinator retirements,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<ReleaseManualInventoryBlockCommand, ManualInventoryBlockMutationReceiptDto>
{
    public async Task<Result<ManualInventoryBlockMutationReceiptDto>> HandleAsync(
        ReleaseManualInventoryBlockCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OperationId == Guid.Empty)
        {
            return Result.Failure<ManualInventoryBlockMutationReceiptDto>(
                InventoryApplicationErrors.ManagementOperationInvalid);
        }

        string fingerprint = InventoryManagementMutationFingerprint
            .ComputeManualBlockRelease(
                command.PropertyId,
                command.BlockId,
                command.ExpectedVersion);
        await mutations.AcquireBlockAsync(
                command.BlockId,
                cancellationToken)
            .ConfigureAwait(false);
        InventoryManagementReplayDecision<
            ManualInventoryBlockMutationReceiptDto> replay = await journal
                .InspectBlockReleaseAsync(
                    command.PropertyId,
                    command.BlockId,
                    command.OperationId,
                    command.ExpectedVersion,
                    fingerprint,
                    cancellationToken)
                .ConfigureAwait(false);
        if (replay.Exists)
        {
            return replay.ToResult();
        }

        ManualInventoryBlockIdentity? identity = await blocks
            .GetIdentityAsync(
                command.PropertyId,
                command.BlockId,
                cancellationToken)
            .ConfigureAwait(false);
        if (identity is null)
        {
            return Result.Failure<ManualInventoryBlockMutationReceiptDto>(
                InventoryApplicationErrors.BlockNotFound);
        }

        await mutations.AcquireBlockGroupAsync(
                identity.BlockGroupId,
                cancellationToken)
            .ConfigureAwait(false);
        ManualInventoryBlock? block = await blocks
            .GetAsync(command.PropertyId, command.BlockId, cancellationToken)
            .ConfigureAwait(false);
        if (block is null)
        {
            return Result.Failure<ManualInventoryBlockMutationReceiptDto>(InventoryApplicationErrors.BlockNotFound);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result released = block.Release(
            command.ExpectedVersion,
            idGenerator.NewId(),
            nowUtc,
            command.ActorId);
        if (released.IsFailure)
        {
            return Result.Failure<ManualInventoryBlockMutationReceiptDto>(released.Error);
        }

        await availability.TouchUnitsAsync(
            block.PropertyId,
            [block.InventoryUnitId],
            cancellationToken).ConfigureAwait(false);
        await retirements.TryAdvanceForUnitsAsync(
            block.PropertyId,
            [block.InventoryUnitId],
            excludedAllocationId: null,
            excludedBlockIds: [block.Id],
            cancellationToken).ConfigureAwait(false);
        ManualInventoryBlockMutationReceiptDto receipt = await journal
            .RecordBlockReleaseAsync(
                block,
                command.OperationId,
                command.ExpectedVersion,
                fingerprint,
                nowUtc,
                cancellationToken)
            .ConfigureAwait(false);
        return Result.Success(receipt);
    }
}
