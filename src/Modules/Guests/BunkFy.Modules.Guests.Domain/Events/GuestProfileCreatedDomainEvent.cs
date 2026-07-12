namespace BunkFy.Modules.Guests.Domain.Events;

using Gma.Framework.Domain;
using BunkFy.Modules.Guests.Domain.Aggregates;

public sealed record GuestProfileCreatedDomainEvent : ScopedDomainEvent
{
    public GuestProfileCreatedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string scopeId,
        Guid guestId,
        Guid originPropertyId,
        GuestProfileState status,
        long guestVersion)
        : base(eventId, occurredAtUtc, scopeId)
    {
        this.GuestId = guestId;
        this.OriginPropertyId = originPropertyId;
        this.Status = status;
        this.GuestVersion = guestVersion;
    }

    public Guid GuestId { get; }
    public Guid OriginPropertyId { get; }
    public GuestProfileState Status { get; }
    public long GuestVersion { get; }
}
