namespace BunkFy.Modules.Staff.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Naming;
using Gma.Framework.Scoping;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[ScopeAware]
public sealed record StaffMemberUpdatedIntegrationEvent : IntegrationEvent, IScopedIntegrationEvent
{
    public const string EventType = "member-updated";
    public const int EventVersion = 1;

    public StaffMemberUpdatedIntegrationEvent(Guid eventId, string scopeId, DateTimeOffset occurredAtUtc,
        Guid staffMemberId, StaffStatus status, long staffVersion)
        : base(eventId, occurredAtUtc, EventType, EventVersion)
    {
        this.ScopeId = ScopeIds.Normalize(scopeId, nameof(scopeId));
        this.StaffMemberId = IntegrationEventContractGuards.RequireId(staffMemberId, nameof(staffMemberId));
        this.Status = StaffMemberCreatedIntegrationEvent.RequireStatus(status);
        this.StaffVersion = staffVersion > 0 ? staffVersion : throw new ArgumentOutOfRangeException(nameof(staffVersion));
    }

    public string ScopeId { get; }
    public Guid StaffMemberId { get; }
    public StaffStatus Status { get; }
    public long StaffVersion { get; }
    string IScopedIntegrationEvent.ScopeId => this.ScopeId;
}
