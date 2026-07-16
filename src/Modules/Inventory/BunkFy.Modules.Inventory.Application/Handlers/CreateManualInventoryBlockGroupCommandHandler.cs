namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class CreateManualInventoryBlockGroupCommandHandler(ManualInventoryBlockCreator creator)
    : ICommandHandler<CreateManualInventoryBlockGroupCommand, ManualInventoryBlockGroupDto>
{
    public Task<Result<ManualInventoryBlockGroupDto>> HandleAsync(
        CreateManualInventoryBlockGroupCommand command,
        CancellationToken cancellationToken) =>
        creator.CreateAsync(
            command.PropertyId,
            command.Target,
            command.Arrival,
            command.Departure,
            command.Reason,
            command.ActorId,
            cancellationToken);
}
