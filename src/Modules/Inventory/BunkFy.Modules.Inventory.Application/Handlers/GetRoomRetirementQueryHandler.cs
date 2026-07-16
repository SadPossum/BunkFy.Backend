namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Application.Queries;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class GetRoomRetirementQueryHandler(
    IRoomRetirementRepository retirements,
    RoomRetirementCoordinator coordinator)
    : IQueryHandler<GetRoomRetirementQuery, RoomRetirementDto>
{
    public async Task<Result<RoomRetirementDto>> HandleAsync(
        GetRoomRetirementQuery query,
        CancellationToken cancellationToken)
    {
        RoomRetirementProcess? process = await retirements
            .GetAsync(query.PropertyId, query.TopologyChangeId, cancellationToken)
            .ConfigureAwait(false);
        return process is null
            ? Result.Failure<RoomRetirementDto>(InventoryApplicationErrors.RoomRetirementNotFound)
            : Result.Success(await coordinator.GetDtoAsync(process, cancellationToken).ConfigureAwait(false));
    }
}
