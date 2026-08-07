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
        long guestVersion,
        Guid? creationConfirmationId = null)
        : base(eventId, occurredAtUtc, scopeId)
    {
        this.GuestId = guestId;
        this.OriginPropertyId = originPropertyId;
        this.Status = status;
        this.GuestVersion = guestVersion;
        this.CreationConfirmationId = creationConfirmationId switch
        {
            Guid value when value != Guid.Empty => value,
            null => null,
            _ => throw new ArgumentException(
                "Creation confirmation id must not be empty when provided.",
                nameof(creationConfirmationId))
        };
    }

    public Guid GuestId { get; }
    public Guid OriginPropertyId { get; }
    public GuestProfileState Status { get; }
    public long GuestVersion { get; }
    public Guid? CreationConfirmationId { get; }
}
