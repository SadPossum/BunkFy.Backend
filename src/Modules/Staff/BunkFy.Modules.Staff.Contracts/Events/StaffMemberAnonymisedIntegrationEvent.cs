namespace BunkFy.Modules.Staff.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Naming;
using Gma.Framework.Scoping;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[ScopeAware]
public sealed record StaffMemberAnonymisedIntegrationEvent
    : IntegrationEvent, IScopedIntegrationEvent
{
    public const string EventType = "member-anonymised";
    public const int EventVersion = 1;

    public StaffMemberAnonymisedIntegrationEvent(
        Guid eventId,
        string scopeId,
        DateTimeOffset occurredAtUtc,
        Guid staffMemberId,
        long staffVersion)
        : base(eventId, occurredAtUtc, EventType, EventVersion)
    {
        this.ScopeId = ScopeIds.Normalize(scopeId, nameof(scopeId));
        this.StaffMemberId = IntegrationEventContractGuards.RequireId(
            staffMemberId,
            nameof(staffMemberId));
        this.StaffVersion = staffVersion > 0
            ? staffVersion
            : throw new ArgumentOutOfRangeException(
                nameof(staffVersion));
    }

    public string ScopeId { get; }
    public Guid StaffMemberId { get; }
    public long StaffVersion { get; }

    string IScopedIntegrationEvent.ScopeId => this.ScopeId;
}
