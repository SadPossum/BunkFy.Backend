namespace BunkFy.Modules.Workspaces.Persistence;

using Gma.Framework.Domain;
using Gma.Framework.ProjectionRebuild;

public sealed class WorkspaceProjectionRebuildCheckpoint
    : ProjectionRebuildCheckpointState, IScopedEntity
{
    private WorkspaceProjectionRebuildCheckpoint() { }

    private WorkspaceProjectionRebuildCheckpoint(
        ProjectionRebuildCheckpointKey key,
        ProjectionRebuildCheckpoint checkpoint)
        : base(key, checkpoint, scopeAware: true) { }

    public static WorkspaceProjectionRebuildCheckpoint Create(
        ProjectionRebuildCheckpointKey key,
        ProjectionRebuildCheckpoint checkpoint) => new(key, checkpoint);

    internal static WorkspaceProjectionRebuildCheckpoint CreateEmpty() => new();
}
