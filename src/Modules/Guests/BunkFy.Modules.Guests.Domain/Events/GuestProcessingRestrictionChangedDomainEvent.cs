namespace BunkFy.Modules.Guests.Domain.Events;

using Gma.Framework.Domain;

public sealed record GuestProcessingRestrictionChangedDomainEvent : ScopedDomainEvent
{
    public GuestProcessingRestrictionChangedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string scopeId,
        Guid propertyId,
        Guid guestId,
        int contractVersion,
        long projectionRevision,
        bool isRestricted)
        : base(eventId, occurredAtUtc, scopeId)
    {
        this.PropertyId = propertyId;
        this.GuestId = guestId;
        this.ContractVersion = contractVersion;
        this.ProjectionRevision = projectionRevision;
        this.IsRestricted = isRestricted;
    }

    public Guid PropertyId { get; }
    public Guid GuestId { get; }
    public int ContractVersion { get; }
    public long ProjectionRevision { get; }
    public bool IsRestricted { get; }
}
