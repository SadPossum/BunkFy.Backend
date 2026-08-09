namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class CreateManualInventoryBlockCommandHandler(
    InventoryManagementMutationCoordinator mutations,
    InventoryManagementOperationJournal journal,
    ManualInventoryBlockCreator creator)
    : ICommandHandler<CreateManualInventoryBlockCommand, ManualInventoryBlockMutationReceiptDto>
{
    public async Task<Result<ManualInventoryBlockMutationReceiptDto>> HandleAsync(
        CreateManualInventoryBlockCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OperationId == Guid.Empty)
        {
            return Result.Failure<ManualInventoryBlockMutationReceiptDto>(
                InventoryApplicationErrors.ManagementOperationInvalid);
        }

        InventoryBlockTarget target = new(
            InventoryBlockTargetKind.Unit,
            InventoryUnitId: command.InventoryUnitId);
        string fingerprint = InventoryManagementMutationFingerprint
            .ComputeManualBlockCreate(
                command.PropertyId,
                target,
                command.Arrival,
                command.Departure,
                command.Reason,
                group: false);
        await mutations.AcquirePropertyOperationAsync(
                command.PropertyId,
                command.OperationId,
                cancellationToken)
            .ConfigureAwait(false);
        InventoryManagementReplayDecision<
            ManualInventoryBlockMutationReceiptDto> replay = await journal
                .InspectBlockCreateAsync(
                    command.PropertyId,
                    command.OperationId,
                    fingerprint,
                    cancellationToken)
                .ConfigureAwait(false);
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
            return Result.Failure<ManualInventoryBlockMutationReceiptDto>(
                result.Error);
        }

        ManualInventoryBlockMutationReceiptDto receipt = await journal
            .RecordBlockCreateAsync(
                result.Value,
                command.OperationId,
                fingerprint,
                result.Value.Blocks.Single().CreatedAtUtc,
                cancellationToken)
            .ConfigureAwait(false);
        return Result.Success(receipt);
    }
}
