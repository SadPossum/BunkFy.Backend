namespace BunkFy.Modules.Inventory.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using BunkFy.Modules.Inventory.Domain.Errors;
using BunkFy.Modules.Properties.Contracts;

internal sealed class ConfigureRoomSalesModeCommandHandler(
    IInventoryTopologyRepository topologyRepository,
    IRoomInventoryConfigurationRepository configurationRepository,
    IInventoryAvailabilityRepository availability,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<ConfigureRoomSalesModeCommand, RoomInventoryMutationReceiptDto>
{
    public async Task<Result<RoomInventoryMutationReceiptDto>> HandleAsync(
        ConfigureRoomSalesModeCommand command,
        CancellationToken cancellationToken)
    {
        InventoryRoomTopologySnapshot? topology = await topologyRepository
            .GetRoomAsync(command.PropertyId, command.RoomId, cancellationToken)
            .ConfigureAwait(false);
        if (topology is null)
        {
            return Result.Failure<RoomInventoryMutationReceiptDto>(InventoryDomainErrors.RoomNotFound);
        }

        if (topology.Status == RoomStatus.Retired)
        {
            return Result.Failure<RoomInventoryMutationReceiptDto>(InventoryDomainErrors.RoomRetired);
        }

        if (command.SalesMode == InventorySalesMode.BedLevel && topology.ActiveBedCount == 0)
        {
            return Result.Failure<RoomInventoryMutationReceiptDto>(InventoryDomainErrors.BedLevelRequiresBeds);
        }

        RoomInventoryConfiguration? configuration = await configurationRepository
            .GetAsync(command.PropertyId, command.RoomId, cancellationToken)
            .ConfigureAwait(false);
        if (configuration is null)
        {
            return Result.Failure<RoomInventoryMutationReceiptDto>(InventoryDomainErrors.RoomNotFound);
        }

        RoomSalesMode salesMode = command.SalesMode == InventorySalesMode.RoomLevel
            ? RoomSalesMode.RoomLevel
            : RoomSalesMode.BedLevel;
        if (configuration.SalesMode != salesMode)
        {
            RoomInventoryImpactSnapshot? impact = await availability
                .GetRoomImpactAsync(command.PropertyId, command.RoomId, cancellationToken)
                .ConfigureAwait(false);
            if (impact?.PreventsSalesModeChange == true)
            {
                return Result.Failure<RoomInventoryMutationReceiptDto>(InventoryApplicationErrors.RoomHasActiveClaims);
            }
        }

        Result result = configuration.Configure(
            salesMode,
            command.ExpectedVersion,
            idGenerator.NewId(),
            clock.UtcNow,
            command.ActorId);
        if (result.IsFailure)
        {
            return Result.Failure<RoomInventoryMutationReceiptDto>(result.Error);
        }

        InventorySalesMode configuredMode = configuration.SalesMode switch
        {
            RoomSalesMode.RoomLevel => InventorySalesMode.RoomLevel,
            RoomSalesMode.BedLevel => InventorySalesMode.BedLevel,
            _ => InventorySalesMode.Unknown
        };
        return Result.Success(new RoomInventoryMutationReceiptDto(
            configuration.PropertyId,
            configuration.Id,
            configuredMode,
            configuration.Version));
    }
}
