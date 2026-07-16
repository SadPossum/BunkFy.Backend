namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class RequestBedRetirementCommandHandler(
    IInventoryReadRepository inventory,
    IBedRetirementRepository retirements,
    IRoomRetirementRepository roomRetirements,
    IInventoryAvailabilityRepository availability,
    BedRetirementCoordinator coordinator,
    InventoryUnitDefinitionPublisher definitions,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<RequestBedRetirementCommand, BedRetirementDto>
{
    public async Task<Result<BedRetirementDto>> HandleAsync(
        RequestBedRetirementCommand command,
        CancellationToken cancellationToken)
    {
        string? scopeId = scopeContext.ScopeId;
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeId))
        {
            return Result.Failure<BedRetirementDto>(InventoryApplicationErrors.TenantRequired);
        }

        InventoryUnitSnapshot? unit = await inventory.GetUnitAsync(
            command.PropertyId,
            command.BedId,
            cancellationToken).ConfigureAwait(false);
        if (unit is null || unit.Unit.RoomId != command.RoomId || unit.Unit.Kind != InventoryUnitKind.Bed)
        {
            return Result.Failure<BedRetirementDto>(InventoryApplicationErrors.InventoryUnitNotFound);
        }

        BedRetirementProcess? existing = await retirements
            .GetByBedAsync(command.PropertyId, command.BedId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return Result.Success(await coordinator.GetDtoAsync(existing, cancellationToken).ConfigureAwait(false));
        }

        if (!unit.Unit.IsTopologyActive)
        {
            return Result.Failure<BedRetirementDto>(InventoryApplicationErrors.InventoryUnitInactive);
        }

        RoomRetirementProcess? roomRetirement = await roomRetirements
            .GetByRoomAsync(command.PropertyId, command.RoomId, cancellationToken)
            .ConfigureAwait(false);
        if (roomRetirement is not null && RoomRetirementProcess.IsDrainActive(roomRetirement.State))
        {
            return Result.Failure<BedRetirementDto>(InventoryApplicationErrors.RoomRetirementInProgress);
        }

        Result<BedRetirementProcess> created = BedRetirementProcess.Create(
            idGenerator.NewId(),
            scopeId,
            command.PropertyId,
            command.RoomId,
            command.BedId,
            command.Reason,
            command.RequestedBy,
            clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<BedRetirementDto>(created.Error);
        }

        await retirements.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
        await availability.TouchUnitsAsync(
            command.PropertyId,
            [command.BedId],
            cancellationToken).ConfigureAwait(false);
        BedRetirementDto result = await coordinator.TryAdvanceAsync(
            created.Value,
            excludedAllocationId: null,
            excludedBlockIds: [],
            cancellationToken).ConfigureAwait(false);
        await definitions.PublishRoomAsync(
            command.PropertyId,
            command.RoomId,
            clock.UtcNow,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(result);
    }
}
