namespace BunkFy.Modules.Staff.Domain.Events;

using Gma.Framework.Domain;

public sealed record StaffMemberAnonymisedDomainEvent : ScopedDomainEvent
{
    public StaffMemberAnonymisedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string scopeId,
        Guid staffMemberId,
        long staffVersion)
        : base(eventId, occurredAtUtc, scopeId)
    {
        this.StaffMemberId = staffMemberId;
        this.StaffVersion = staffVersion;
    }

    public Guid StaffMemberId { get; }
    public long StaffVersion { get; }
}
