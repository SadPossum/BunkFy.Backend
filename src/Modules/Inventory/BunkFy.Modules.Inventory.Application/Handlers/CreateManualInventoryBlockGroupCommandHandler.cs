namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class CreateManualInventoryBlockGroupCommandHandler(
    InventoryManagementMutationCoordinator mutations,
    InventoryManagementOperationJournal journal,
    ManualInventoryBlockCreator creator)
    : ICommandHandler<CreateManualInventoryBlockGroupCommand, ManualInventoryBlockGroupMutationReceiptDto>
{
    public async Task<Result<ManualInventoryBlockGroupMutationReceiptDto>> HandleAsync(
        CreateManualInventoryBlockGroupCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OperationId == Guid.Empty)
        {
            return Result.Failure<
                ManualInventoryBlockGroupMutationReceiptDto>(
                InventoryApplicationErrors.ManagementOperationInvalid);
        }

        if (!InventoryBlockTargetNormalizer.TryNormalize(
                command.Target,
                out InventoryBlockTarget target))
        {
            return Result.Failure<
                ManualInventoryBlockGroupMutationReceiptDto>(
                InventoryApplicationErrors.BlockTargetInvalid);
        }

        string fingerprint = InventoryManagementMutationFingerprint
            .ComputeManualBlockCreate(
                command.PropertyId,
                target,
                command.Arrival,
                command.Departure,
                command.Reason,
                group: true);
        await mutations.AcquirePropertyOperationAsync(
                command.PropertyId,
                command.OperationId,
                cancellationToken)
            .ConfigureAwait(false);
        InventoryManagementReplayDecision<
            ManualInventoryBlockGroupMutationReceiptDto> replay =
            await journal.InspectBlockGroupCreateAsync(
                command.PropertyId,
                command.OperationId,
                fingerprint,
                cancellationToken).ConfigureAwait(false);
        if (replay.Exists)
        {
            return replay.ToResult();
        }

        Result<ManualInventoryBlockCreationResult> result = await creator.CreateAsync(
            command.PropertyId,
            target,
            command.Arrival,
            command.Departure,
            command.Reason,
            command.ActorId,
            cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return Result.Failure<
                ManualInventoryBlockGroupMutationReceiptDto>(result.Error);
        }

        ManualInventoryBlockGroupMutationReceiptDto receipt = await journal
            .RecordBlockGroupCreateAsync(
                result.Value,
                command.OperationId,
                fingerprint,
                result.Value.Blocks.First().CreatedAtUtc,
                cancellationToken)
            .ConfigureAwait(false);
        return Result.Success(receipt);
    }
}
