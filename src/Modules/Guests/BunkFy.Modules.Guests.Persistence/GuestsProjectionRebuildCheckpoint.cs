namespace BunkFy.Modules.Guests.Persistence;

using Gma.Framework.Domain;
using Gma.Framework.ProjectionRebuild;

public sealed class GuestsProjectionRebuildCheckpoint : ProjectionRebuildCheckpointState, IScopedEntity
{
    private GuestsProjectionRebuildCheckpoint() { }

    private GuestsProjectionRebuildCheckpoint(
        ProjectionRebuildCheckpointKey key,
        ProjectionRebuildCheckpoint checkpoint)
        : base(key, checkpoint, scopeAware: true)
    {
    }

    public static GuestsProjectionRebuildCheckpoint Create(
        ProjectionRebuildCheckpointKey key,
        ProjectionRebuildCheckpoint checkpoint) =>
        new(key, checkpoint);

    internal static GuestsProjectionRebuildCheckpoint CreateEmpty() => new();
}
