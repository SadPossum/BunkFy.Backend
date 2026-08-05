namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class CreateManualInventoryBlockCommandHandler(ManualInventoryBlockCreator creator)
    : ICommandHandler<CreateManualInventoryBlockCommand, ManualInventoryBlockMutationReceiptDto>
{
    public async Task<Result<ManualInventoryBlockMutationReceiptDto>> HandleAsync(
        CreateManualInventoryBlockCommand command,
        CancellationToken cancellationToken)
    {
        Result<ManualInventoryBlockCreationResult> result = await creator.CreateAsync(
            command.PropertyId,
            new InventoryBlockTarget(
                InventoryBlockTargetKind.Unit,
                InventoryUnitId: command.InventoryUnitId),
            command.Arrival,
            command.Departure,
            command.Reason,
            command.ActorId,
            cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result.Failure<ManualInventoryBlockMutationReceiptDto>(result.Error)
            : Result.Success(result.Value.Blocks.Single().ToMutationReceipt());
    }
}
