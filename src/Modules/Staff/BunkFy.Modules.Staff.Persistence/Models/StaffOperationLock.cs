namespace BunkFy.Modules.Staff.Persistence.Models;

using Gma.Framework.Domain.Models;

internal sealed class StaffOperationLock : ScopedEntity<Guid>
{
    private StaffOperationLock() { }

    public StaffOperationLock(
        Guid id,
        string scopeId,
        Guid staffMemberId)
        : base(id, scopeId)
    {
        if (id == Guid.Empty ||
            staffMemberId == Guid.Empty ||
            id != staffMemberId)
        {
            throw new ArgumentException(
                "The Staff operation-lock coordinate is invalid.");
        }

        this.StaffMemberId = staffMemberId;
    }

    public Guid StaffMemberId { get; private set; }
    public long Revision { get; private set; } = 1;

    public void Touch() =>
        this.Revision = checked(this.Revision + 1);
}
