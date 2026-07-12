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
    IManualInventoryBlockRepository blocks,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<ReleaseManualInventoryBlockCommand, ManualInventoryBlockDto>
{
    public async Task<Result<ManualInventoryBlockDto>> HandleAsync(
        ReleaseManualInventoryBlockCommand command,
        CancellationToken cancellationToken)
    {
        ManualInventoryBlock? block = await blocks
            .GetAsync(command.PropertyId, command.BlockId, cancellationToken)
            .ConfigureAwait(false);
        if (block is null)
        {
            return Result.Failure<ManualInventoryBlockDto>(InventoryApplicationErrors.BlockNotFound);
        }

        Result released = block.Release(command.ExpectedVersion, idGenerator.NewId(), clock.UtcNow);
        if (released.IsFailure)
        {
            return Result.Failure<ManualInventoryBlockDto>(released.Error);
        }

        await blocks.TouchUnitAsync(block.InventoryUnitId, cancellationToken).ConfigureAwait(false);
        return Result.Success(block.ToDto());
    }
}
