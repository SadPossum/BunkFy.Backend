namespace BunkFy.Modules.Inventory.Application.Commands;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Inventory.Contracts;

public sealed record CreateManualInventoryBlockCommand(
    Guid PropertyId,
    Guid InventoryUnitId,
    DateOnly Arrival,
    DateOnly Departure,
    string Reason)
    : ITransactionalCommand<ManualInventoryBlockDto>;
