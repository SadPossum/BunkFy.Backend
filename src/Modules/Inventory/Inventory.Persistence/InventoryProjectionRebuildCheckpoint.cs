namespace Inventory.Persistence;

using Gma.Framework.Domain;
using Gma.Framework.ProjectionRebuild;

public sealed class InventoryProjectionRebuildCheckpoint : ProjectionRebuildCheckpointState, IScopedEntity
{
    private InventoryProjectionRebuildCheckpoint() { }

    private InventoryProjectionRebuildCheckpoint(
        ProjectionRebuildCheckpointKey key,
        ProjectionRebuildCheckpoint checkpoint)
        : base(key, checkpoint, scopeAware: true)
    {
    }

    public static InventoryProjectionRebuildCheckpoint Create(
        ProjectionRebuildCheckpointKey key,
        ProjectionRebuildCheckpoint checkpoint) =>
        new(key, checkpoint);

    internal static InventoryProjectionRebuildCheckpoint CreateEmpty() => new();
}
