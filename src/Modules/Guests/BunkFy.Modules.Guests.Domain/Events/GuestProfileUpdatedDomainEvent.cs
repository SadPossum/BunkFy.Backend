namespace BunkFy.Modules.Guests.Domain.Events;

using Gma.Framework.Domain;
using BunkFy.Modules.Guests.Domain.Aggregates;

public sealed record GuestProfileUpdatedDomainEvent : ScopedDomainEvent
{
    public GuestProfileUpdatedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string scopeId,
        Guid guestId,
        GuestProfileState status,
        long guestVersion)
        : base(eventId, occurredAtUtc, scopeId)
    {
        this.GuestId = guestId;
        this.Status = status;
        this.GuestVersion = guestVersion;
    }

    public Guid GuestId { get; }
    public GuestProfileState Status { get; }
    public long GuestVersion { get; }
}
