namespace BunkFy.Modules.Guests.Persistence.Models;

using Gma.Framework.Domain.Models;

internal sealed class GuestOperationLock : ScopedEntity<Guid>
{
    private GuestOperationLock() { }

    public GuestOperationLock(
        Guid id,
        string scopeId,
        GuestOperationLockKind resourceKind,
        Guid resourceId)
        : base(id, scopeId)
    {
        if (id == Guid.Empty ||
            resourceKind == GuestOperationLockKind.Unknown ||
            !Enum.IsDefined(resourceKind) ||
            resourceId == Guid.Empty)
        {
            throw new ArgumentException("The Guest operation-lock coordinate is invalid.");
        }

        this.ResourceKind = resourceKind;
        this.ResourceId = resourceId;
    }

    public GuestOperationLockKind ResourceKind { get; private set; }
    public Guid ResourceId { get; private set; }
    public long Revision { get; private set; } = 1;

    public void Touch() => this.Revision = checked(this.Revision + 1);
}

internal enum GuestOperationLockKind
{
    Unknown = 0,
    Guest = 1,
    Property = 2
}
