namespace Inventory.Application.Queries;

using Gma.Framework.Cqrs;
using Inventory.Contracts;

public sealed record GetInventoryAvailabilityQuery(
    Guid PropertyId,
    DateOnly Arrival,
    DateOnly Departure)
    : IQuery<InventoryAvailabilityResponse>;
