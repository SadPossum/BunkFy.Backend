namespace Inventory.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Inventory.Application.Commands;
using Inventory.Application.Ports;
using Inventory.Contracts;
using Inventory.Domain.Aggregates;

internal sealed class CreateManualInventoryBlockCommandHandler(
    IInventoryReadRepository inventory,
    IManualInventoryBlockRepository blocks,
    IInventoryAllocationRepository allocations,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<CreateManualInventoryBlockCommand, ManualInventoryBlockDto>
{
    public async Task<Result<ManualInventoryBlockDto>> HandleAsync(
        CreateManualInventoryBlockCommand command,
        CancellationToken cancellationToken)
    {
        string? scopeId = scopeContext.ScopeId;
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeId))
        {
            return Result.Failure<ManualInventoryBlockDto>(InventoryApplicationErrors.TenantRequired);
        }

        InventoryUnitSnapshot? unit = await inventory
            .GetUnitAsync(command.PropertyId, command.InventoryUnitId, cancellationToken)
            .ConfigureAwait(false);
        if (unit is null)
        {
            return Result.Failure<ManualInventoryBlockDto>(InventoryApplicationErrors.InventoryUnitNotFound);
        }

        if (!unit.Unit.IsTopologyActive)
        {
            return Result.Failure<ManualInventoryBlockDto>(InventoryApplicationErrors.InventoryUnitInactive);
        }

        if (!unit.IsSellable)
        {
            return Result.Failure<ManualInventoryBlockDto>(InventoryApplicationErrors.InventoryUnitNotSellable);
        }

        if (await blocks.HasActiveOverlapAsync(
                command.InventoryUnitId,
                command.Arrival,
                command.Departure,
                cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ManualInventoryBlockDto>(InventoryApplicationErrors.BlockOverlap);
        }

        if (await allocations.HasActiveAllocationConflictAsync(
                [command.InventoryUnitId],
                command.Arrival,
                command.Departure,
                cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ManualInventoryBlockDto>(InventoryApplicationErrors.BlockAllocationConflict);
        }

        Result<ManualInventoryBlock> created = ManualInventoryBlock.Create(
            idGenerator.NewId(),
            scopeId,
            command.PropertyId,
            command.InventoryUnitId,
            command.Arrival,
            command.Departure,
            command.Reason,
            idGenerator.NewId(),
            clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<ManualInventoryBlockDto>(created.Error);
        }

        await blocks.TouchUnitAsync(command.InventoryUnitId, cancellationToken).ConfigureAwait(false);
        await blocks.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(created.Value.ToDto());
    }
}
