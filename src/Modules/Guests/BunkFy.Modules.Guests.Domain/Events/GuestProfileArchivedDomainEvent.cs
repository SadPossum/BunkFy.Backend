namespace BunkFy.Modules.Guests.Domain.Events;

using Gma.Framework.Domain;

public sealed record GuestProfileArchivedDomainEvent : ScopedDomainEvent
{
    public GuestProfileArchivedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string scopeId,
        Guid guestId,
        long guestVersion)
        : base(eventId, occurredAtUtc, scopeId)
    {
        this.GuestId = guestId;
        this.GuestVersion = guestVersion;
    }

    public Guid GuestId { get; }
    public long GuestVersion { get; }
}
