namespace Inventory.Application.Commands;

using Gma.Framework.Cqrs;
using Inventory.Contracts;

public sealed record CreateManualInventoryBlockCommand(
    Guid PropertyId,
    Guid InventoryUnitId,
    DateOnly Arrival,
    DateOnly Departure,
    string Reason)
    : ITransactionalCommand<ManualInventoryBlockDto>;
