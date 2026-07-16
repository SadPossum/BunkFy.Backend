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
        DateOnly effectiveFrom, DateOnly? effectiveTo, long staffVersion, string? actorId = null)
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
        this.ActorId = OptionalActor(actorId);
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
    public string? ActorId { get; }
    string IScopedIntegrationEvent.ScopeId => this.ScopeId;

    private static string? OptionalActor(string? value)
    {
        string? normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is null || normalized.Length <= StaffContractLimits.ActorIdMaxLength
            ? normalized
            : throw new ArgumentException("Actor id is invalid.", nameof(value));
    }
}
