namespace BunkFy.Modules.Properties.Persistence;

using Gma.Framework.Domain.Models;

internal sealed class PropertyOperationLock : ScopedEntity<Guid>
{
    private PropertyOperationLock() { }

    public PropertyOperationLock(Guid propertyId, string scopeId)
        : base(propertyId, scopeId)
    {
        if (propertyId == Guid.Empty)
        {
            throw new ArgumentException(
                "The Property operation-lock coordinate is invalid.");
        }

        this.PropertyId = propertyId;
    }

    public Guid PropertyId { get; private set; }
    public long Revision { get; private set; } = 1;

    public void Touch() =>
        this.Revision = checked(this.Revision + 1);
}
