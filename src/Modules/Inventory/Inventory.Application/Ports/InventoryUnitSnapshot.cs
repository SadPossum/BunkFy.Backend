namespace Inventory.Application.Ports;

using Inventory.Contracts;

public sealed record InventoryUnitSnapshot(InventoryUnitDto Unit, bool IsSellable);
