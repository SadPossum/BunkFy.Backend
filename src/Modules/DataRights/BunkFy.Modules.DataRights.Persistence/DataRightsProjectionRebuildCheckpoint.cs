namespace BunkFy.Modules.DataRights.Persistence;

using Gma.Framework.Domain;
using Gma.Framework.ProjectionRebuild;

public sealed class DataRightsProjectionRebuildCheckpoint
    : ProjectionRebuildCheckpointState, IScopedEntity
{
    private DataRightsProjectionRebuildCheckpoint() { }

    private DataRightsProjectionRebuildCheckpoint(
        ProjectionRebuildCheckpointKey key,
        ProjectionRebuildCheckpoint checkpoint)
        : base(key, checkpoint, scopeAware: true)
    {
    }

    public static DataRightsProjectionRebuildCheckpoint Create(
        ProjectionRebuildCheckpointKey key,
        ProjectionRebuildCheckpoint checkpoint) =>
        new(key, checkpoint);

    internal static DataRightsProjectionRebuildCheckpoint CreateEmpty() => new();
}
