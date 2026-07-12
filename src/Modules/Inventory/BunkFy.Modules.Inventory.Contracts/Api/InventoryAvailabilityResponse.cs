namespace BunkFy.Modules.Inventory.Contracts;

public sealed record InventoryAvailabilityResponse(
    Guid PropertyId,
    DateOnly Arrival,
    DateOnly Departure,
    IReadOnlyCollection<InventoryUnitAvailabilityDto> Units);
