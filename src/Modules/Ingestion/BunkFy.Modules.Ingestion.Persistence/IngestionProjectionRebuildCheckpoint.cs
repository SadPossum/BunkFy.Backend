namespace BunkFy.Modules.Ingestion.Persistence;

using Gma.Framework.Domain;
using Gma.Framework.ProjectionRebuild;

public sealed class IngestionProjectionRebuildCheckpoint : ProjectionRebuildCheckpointState, IScopedEntity
{
    private IngestionProjectionRebuildCheckpoint() { }

    private IngestionProjectionRebuildCheckpoint(
        ProjectionRebuildCheckpointKey key,
        ProjectionRebuildCheckpoint checkpoint)
        : base(key, checkpoint, scopeAware: true)
    {
    }

    public static IngestionProjectionRebuildCheckpoint Create(
        ProjectionRebuildCheckpointKey key,
        ProjectionRebuildCheckpoint checkpoint) => new(key, checkpoint);

    internal static IngestionProjectionRebuildCheckpoint CreateEmpty() => new();
}
