namespace Inventory.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Inventory.Application.Ports;
using Inventory.Application.Queries;
using Inventory.Contracts;
using Inventory.Domain.Errors;

internal sealed class GetInventoryAvailabilityQueryHandler(IInventoryReadRepository inventory)
    : IQueryHandler<GetInventoryAvailabilityQuery, InventoryAvailabilityResponse>
{
    public async Task<Result<InventoryAvailabilityResponse>> HandleAsync(
        GetInventoryAvailabilityQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Arrival >= query.Departure)
        {
            return Result.Failure<InventoryAvailabilityResponse>(InventoryDomainErrors.StayRangeInvalid);
        }

        if (!await inventory.PropertyExistsAsync(query.PropertyId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<InventoryAvailabilityResponse>(InventoryApplicationErrors.PropertyNotFound);
        }

        return Result.Success(await inventory.GetAvailabilityAsync(
            query.PropertyId,
            query.Arrival,
            query.Departure,
            cancellationToken).ConfigureAwait(false));
    }
}
