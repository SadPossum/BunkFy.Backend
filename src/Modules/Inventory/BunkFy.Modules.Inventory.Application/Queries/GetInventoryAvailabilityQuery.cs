namespace BunkFy.Modules.Inventory.Application.Queries;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Inventory.Contracts;

public sealed record GetInventoryAvailabilityQuery(
    Guid PropertyId,
    DateOnly Arrival,
    DateOnly Departure)
    : IQuery<InventoryAvailabilityResponse>;
