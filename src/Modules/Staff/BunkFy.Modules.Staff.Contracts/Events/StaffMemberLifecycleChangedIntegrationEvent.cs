namespace BunkFy.Modules.Staff.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Naming;
using Gma.Framework.Scoping;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[ScopeAware]
public sealed record StaffMemberLifecycleChangedIntegrationEvent : IntegrationEvent, IScopedIntegrationEvent
{
    public const string EventType = "member-lifecycle-changed";
    public const int EventVersion = 1;

    public StaffMemberLifecycleChangedIntegrationEvent(Guid eventId, string scopeId, DateTimeOffset occurredAtUtc,
        Guid staffMemberId, StaffStatus status, DateOnly effectiveOn, long staffVersion, string? actorId = null)
        : base(eventId, occurredAtUtc, EventType, EventVersion)
    {
        this.ScopeId = ScopeIds.Normalize(scopeId, nameof(scopeId));
        this.StaffMemberId = IntegrationEventContractGuards.RequireId(staffMemberId, nameof(staffMemberId));
        this.Status = StaffMemberCreatedIntegrationEvent.RequireStatus(status);
        this.EffectiveOn = effectiveOn;
        this.StaffVersion = staffVersion > 0 ? staffVersion : throw new ArgumentOutOfRangeException(nameof(staffVersion));
        this.ActorId = OptionalActor(actorId);
    }

    public string ScopeId { get; }
    public Guid StaffMemberId { get; }
    public StaffStatus Status { get; }
    public DateOnly EffectiveOn { get; }
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
