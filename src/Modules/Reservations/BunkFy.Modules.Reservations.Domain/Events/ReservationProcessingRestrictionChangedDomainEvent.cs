namespace BunkFy.Modules.Reservations.Domain.Events;

using Gma.Framework.Domain;

public sealed record ReservationProcessingRestrictionChangedDomainEvent : ScopedDomainEvent
{
    public ReservationProcessingRestrictionChangedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string scopeId,
        Guid propertyId,
        Guid reservationId,
        int contractVersion,
        long projectionRevision,
        bool isRestricted)
        : base(eventId, occurredAtUtc, scopeId)
    {
        this.PropertyId = propertyId;
        this.ReservationId = reservationId;
        this.ContractVersion = contractVersion;
        this.ProjectionRevision = projectionRevision;
        this.IsRestricted = isRestricted;
    }

    public Guid PropertyId { get; }
    public Guid ReservationId { get; }
    public int ContractVersion { get; }
    public long ProjectionRevision { get; }
    public bool IsRestricted { get; }
}
