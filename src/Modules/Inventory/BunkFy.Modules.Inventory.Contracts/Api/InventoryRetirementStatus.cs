namespace BunkFy.Modules.Inventory.Contracts;

public enum InventoryRetirementStatus
{
    Unknown = 0,
    Draining = 1,
    FinalizationRequested = 2,
    FinalizedAwaitingTopology = 3,
    Completed = 4,
    Rejected = 5
}
