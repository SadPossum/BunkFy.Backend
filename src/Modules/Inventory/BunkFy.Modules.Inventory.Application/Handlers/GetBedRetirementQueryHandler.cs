namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Application.Queries;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class GetBedRetirementQueryHandler(
    IBedRetirementRepository retirements,
    BedRetirementCoordinator coordinator)
    : IQueryHandler<GetBedRetirementQuery, BedRetirementDto>
{
    public async Task<Result<BedRetirementDto>> HandleAsync(
        GetBedRetirementQuery query,
        CancellationToken cancellationToken)
    {
        BedRetirementProcess? process = await retirements
            .GetAsync(query.PropertyId, query.TopologyChangeId, cancellationToken)
            .ConfigureAwait(false);
        return process is null
            ? Result.Failure<BedRetirementDto>(InventoryApplicationErrors.BedRetirementNotFound)
            : Result.Success(await coordinator.GetDtoAsync(process, cancellationToken).ConfigureAwait(false));
    }
}
