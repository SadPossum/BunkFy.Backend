namespace BunkFy.Modules.Staff.Persistence;

using Gma.Framework.Domain;
using Gma.Framework.ProjectionRebuild;

public sealed class StaffProjectionRebuildCheckpoint : ProjectionRebuildCheckpointState, IScopedEntity
{
    private StaffProjectionRebuildCheckpoint() { }
    private StaffProjectionRebuildCheckpoint(ProjectionRebuildCheckpointKey key,
        ProjectionRebuildCheckpoint checkpoint) : base(key, checkpoint, scopeAware: true) { }

    public static StaffProjectionRebuildCheckpoint Create(ProjectionRebuildCheckpointKey key,
        ProjectionRebuildCheckpoint checkpoint) => new(key, checkpoint);
    internal static StaffProjectionRebuildCheckpoint CreateEmpty() => new();
}
