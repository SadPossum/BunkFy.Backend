namespace BunkFy.Modules.Staff.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Naming;
using Gma.Framework.Scoping;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[ScopeAware]
public sealed record StaffAuthSubjectChangedIntegrationEvent : IntegrationEvent, IScopedIntegrationEvent
{
    public const string EventType = "auth-subject-changed";
    public const int EventVersion = 1;

    public StaffAuthSubjectChangedIntegrationEvent(Guid eventId, string scopeId, DateTimeOffset occurredAtUtc,
        Guid staffMemberId, string? authSubjectId, long staffVersion)
        : base(eventId, occurredAtUtc, EventType, EventVersion)
    {
        this.ScopeId = ScopeIds.Normalize(scopeId, nameof(scopeId));
        this.StaffMemberId = IntegrationEventContractGuards.RequireId(staffMemberId, nameof(staffMemberId));
        this.AuthSubjectId = StaffMemberCreatedIntegrationEvent.NormalizeOptional(authSubjectId, StaffContractLimits.AuthSubjectIdMaxLength, nameof(authSubjectId));
        this.StaffVersion = staffVersion > 0 ? staffVersion : throw new ArgumentOutOfRangeException(nameof(staffVersion));
    }

    public string ScopeId { get; }
    public Guid StaffMemberId { get; }
    public string? AuthSubjectId { get; }
    public long StaffVersion { get; }
    string IScopedIntegrationEvent.ScopeId => this.ScopeId;
}
