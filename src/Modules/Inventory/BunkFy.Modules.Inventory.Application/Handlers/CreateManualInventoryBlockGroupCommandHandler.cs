namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class CreateManualInventoryBlockGroupCommandHandler(ManualInventoryBlockCreator creator)
    : ICommandHandler<CreateManualInventoryBlockGroupCommand, ManualInventoryBlockGroupMutationReceiptDto>
{
    public async Task<Result<ManualInventoryBlockGroupMutationReceiptDto>> HandleAsync(
        CreateManualInventoryBlockGroupCommand command,
        CancellationToken cancellationToken)
    {
        Result<ManualInventoryBlockCreationResult> result = await creator.CreateAsync(
            command.PropertyId,
            command.Target,
            command.Arrival,
            command.Departure,
            command.Reason,
            command.ActorId,
            cancellationToken).ConfigureAwait(false);
        return result.IsFailure
            ? Result.Failure<ManualInventoryBlockGroupMutationReceiptDto>(result.Error)
            : Result.Success(result.Value.ToMutationReceipt());
    }
}
