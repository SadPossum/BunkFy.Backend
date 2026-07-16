namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class RequestRoomRetirementCommandHandler(
    IInventoryTopologyRepository topology,
    IInventoryReadRepository inventory,
    IRoomRetirementRepository retirements,
    IInventoryAvailabilityRepository availability,
    RoomRetirementCoordinator coordinator,
    InventoryUnitDefinitionPublisher definitions,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<RequestRoomRetirementCommand, RoomRetirementDto>
{
    public async Task<Result<RoomRetirementDto>> HandleAsync(
        RequestRoomRetirementCommand command,
        CancellationToken cancellationToken)
    {
        string? scopeId = scopeContext.ScopeId;
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeId))
        {
            return Result.Failure<RoomRetirementDto>(InventoryApplicationErrors.TenantRequired);
        }

        RoomRetirementProcess? existing = await retirements
            .GetByRoomAsync(command.PropertyId, command.RoomId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return Result.Success(await coordinator.GetDtoAsync(existing, cancellationToken).ConfigureAwait(false));
        }

        InventoryRoomTopologySnapshot? roomTopology = await topology
            .GetRoomAsync(command.PropertyId, command.RoomId, cancellationToken)
            .ConfigureAwait(false);
        if (roomTopology is null)
        {
            return Result.Failure<RoomRetirementDto>(InventoryApplicationErrors.RoomNotFound);
        }

        if (roomTopology.Status == RoomStatus.Retired)
        {
            return Result.Failure<RoomRetirementDto>(InventoryApplicationErrors.RoomRetired);
        }

        RoomInventoryImpactSnapshot? impact = await availability
            .GetRoomImpactAsync(command.PropertyId, command.RoomId, cancellationToken)
            .ConfigureAwait(false);
        if (impact is null)
        {
            return Result.Failure<RoomRetirementDto>(InventoryApplicationErrors.RoomNotFound);
        }

        if (impact.ActiveBedRetirementCount > 0)
        {
            return Result.Failure<RoomRetirementDto>(InventoryApplicationErrors.BedRetirementInProgress);
        }

        RoomInventoryDto? room = await inventory
            .GetRoomAsync(command.PropertyId, command.RoomId, cancellationToken)
            .ConfigureAwait(false);
        if (room is null)
        {
            return Result.Failure<RoomRetirementDto>(InventoryApplicationErrors.RoomNotFound);
        }

        Guid[] unitIds = room.Units
            .Select(unit => unit.InventoryUnitId)
            .Distinct()
            .Order()
            .ToArray();
        if (unitIds.Length == 0)
        {
            return Result.Failure<RoomRetirementDto>(InventoryApplicationErrors.RoomNotFound);
        }

        Result<RoomRetirementProcess> created = RoomRetirementProcess.Create(
            idGenerator.NewId(),
            scopeId,
            command.PropertyId,
            command.RoomId,
            command.Reason,
            command.RequestedBy,
            clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<RoomRetirementDto>(created.Error);
        }

        await retirements.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
        await availability.TouchUnitsAsync(command.PropertyId, unitIds, cancellationToken).ConfigureAwait(false);
        RoomRetirementDto result = await coordinator.TryAdvanceAsync(
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
