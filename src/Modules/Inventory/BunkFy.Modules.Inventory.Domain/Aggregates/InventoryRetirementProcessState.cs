namespace BunkFy.Modules.Inventory.Domain.Aggregates;

public enum InventoryRetirementProcessState
{
    Unknown = 0,
    Draining = 1,
    FinalizationRequested = 2,
    FinalizedAwaitingTopology = 3,
    Completed = 4,
    Rejected = 5
}
