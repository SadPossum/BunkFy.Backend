namespace Inventory.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Inventory.Application.Commands;
using Inventory.Application.Ports;
using Inventory.Contracts;
using Inventory.Domain.Aggregates;
using Inventory.Domain.Errors;
using Properties.Contracts;

internal sealed class ConfigureRoomSalesModeCommandHandler(
    IInventoryTopologyRepository topologyRepository,
    IRoomInventoryConfigurationRepository configurationRepository,
    IInventoryReadRepository readRepository,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<ConfigureRoomSalesModeCommand, RoomInventoryDto>
{
    public async Task<Result<RoomInventoryDto>> HandleAsync(
        ConfigureRoomSalesModeCommand command,
        CancellationToken cancellationToken)
    {
        InventoryRoomTopologySnapshot? topology = await topologyRepository
            .GetRoomAsync(command.PropertyId, command.RoomId, cancellationToken)
            .ConfigureAwait(false);
        if (topology is null)
        {
            return Result.Failure<RoomInventoryDto>(InventoryDomainErrors.RoomNotFound);
        }

        if (topology.Status == RoomStatus.Retired)
        {
            return Result.Failure<RoomInventoryDto>(InventoryDomainErrors.RoomRetired);
        }

        if (command.SalesMode == InventorySalesMode.BedLevel && topology.ActiveBedCount == 0)
        {
            return Result.Failure<RoomInventoryDto>(InventoryDomainErrors.BedLevelRequiresBeds);
        }

        RoomInventoryConfiguration? configuration = await configurationRepository
            .GetAsync(command.PropertyId, command.RoomId, cancellationToken)
            .ConfigureAwait(false);
        if (configuration is null)
        {
            return Result.Failure<RoomInventoryDto>(InventoryDomainErrors.RoomNotFound);
        }

        RoomSalesMode salesMode = command.SalesMode == InventorySalesMode.RoomLevel
            ? RoomSalesMode.RoomLevel
            : RoomSalesMode.BedLevel;
        Result result = configuration.Configure(
            salesMode,
            command.ExpectedVersion,
            idGenerator.NewId(),
            clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<RoomInventoryDto>(result.Error);
        }

        RoomInventoryDto? room = await readRepository
            .GetRoomAsync(command.PropertyId, command.RoomId, cancellationToken)
            .ConfigureAwait(false);
        return room is null
            ? Result.Failure<RoomInventoryDto>(InventoryDomainErrors.RoomNotFound)
            : Result.Success(room);
    }
}
