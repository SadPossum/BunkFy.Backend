namespace BunkFy.Modules.Staff.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Naming;
using Gma.Framework.Scoping;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[ScopeAware]
public sealed record StaffPropertyAssignmentChangedIntegrationEvent : IntegrationEvent, IScopedIntegrationEvent
{
    public const string EventType = "property-assignment-changed";
    public const int EventVersion = 1;

    public StaffPropertyAssignmentChangedIntegrationEvent(Guid eventId, string scopeId, DateTimeOffset occurredAtUtc,
        Guid staffMemberId, Guid assignmentId, Guid propertyId, bool isCurrent, bool isPrimary,
        DateOnly effectiveFrom, DateOnly? effectiveTo, long staffVersion)
        : base(eventId, occurredAtUtc, EventType, EventVersion)
    {
        this.ScopeId = ScopeIds.Normalize(scopeId, nameof(scopeId));
        this.StaffMemberId = IntegrationEventContractGuards.RequireId(staffMemberId, nameof(staffMemberId));
        this.AssignmentId = IntegrationEventContractGuards.RequireId(assignmentId, nameof(assignmentId));
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.IsCurrent = isCurrent;
        this.IsPrimary = isPrimary;
        this.EffectiveFrom = effectiveFrom;
        this.EffectiveTo = effectiveTo;
        this.StaffVersion = staffVersion > 0 ? staffVersion : throw new ArgumentOutOfRangeException(nameof(staffVersion));
    }

    public string ScopeId { get; }
    public Guid StaffMemberId { get; }
    public Guid AssignmentId { get; }
    public Guid PropertyId { get; }
    public bool IsCurrent { get; }
    public bool IsPrimary { get; }
    public DateOnly EffectiveFrom { get; }
    public DateOnly? EffectiveTo { get; }
    public long StaffVersion { get; }
    string IScopedIntegrationEvent.ScopeId => this.ScopeId;
}
