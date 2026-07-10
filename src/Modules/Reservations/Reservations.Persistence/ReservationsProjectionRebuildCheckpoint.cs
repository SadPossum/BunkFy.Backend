namespace Reservations.Persistence;

using Gma.Framework.Domain;
using Gma.Framework.ProjectionRebuild;

public sealed class ReservationsProjectionRebuildCheckpoint : ProjectionRebuildCheckpointState, IScopedEntity
{
    private ReservationsProjectionRebuildCheckpoint() { }

    private ReservationsProjectionRebuildCheckpoint(
        ProjectionRebuildCheckpointKey key,
        ProjectionRebuildCheckpoint checkpoint)
        : base(key, checkpoint, scopeAware: true) { }

    public static ReservationsProjectionRebuildCheckpoint Create(
        ProjectionRebuildCheckpointKey key,
        ProjectionRebuildCheckpoint checkpoint) => new(key, checkpoint);

    internal static ReservationsProjectionRebuildCheckpoint CreateEmpty() => new();
}
