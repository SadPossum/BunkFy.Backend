namespace BunkFy.Modules.Inventory.Application.Ports;

using BunkFy.Modules.Inventory.Contracts;

public sealed record InventoryUnitSnapshot(InventoryUnitDto Unit, bool IsSellable);
