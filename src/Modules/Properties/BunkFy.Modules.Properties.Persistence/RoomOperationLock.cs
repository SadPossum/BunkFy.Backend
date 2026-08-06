namespace BunkFy.Modules.Properties.Persistence;

using Gma.Framework.Domain.Models;

internal sealed class RoomOperationLock : ScopedEntity<Guid>
{
    private RoomOperationLock() { }

    public RoomOperationLock(Guid roomId, string scopeId)
        : base(roomId, scopeId)
    {
        if (roomId == Guid.Empty)
        {
            throw new ArgumentException(
                "The Room operation-lock coordinate is invalid.");
        }

        this.RoomId = roomId;
    }

    public Guid RoomId { get; private set; }
    public long Revision { get; private set; } = 1;

    public void Touch() =>
        this.Revision = checked(this.Revision + 1);
}
