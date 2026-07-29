namespace BunkFy.Modules.Staff.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Naming;
using Gma.Framework.Scoping;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[ScopeAware]
public sealed record StaffProcessingRestrictionChangedIntegrationEvent
    : IntegrationEvent, IScopedIntegrationEvent
{
    public const string EventType = "staff-processing-restriction-changed";
    public const int EventVersion = 1;

    public StaffProcessingRestrictionChangedIntegrationEvent(
        Guid eventId,
        string scopeId,
        DateTimeOffset occurredAtUtc,
        Guid staffMemberId,
        int contractVersion,
        long projectionRevision,
        bool isRestricted)
        : base(eventId, occurredAtUtc, EventType, EventVersion)
    {
        this.ScopeId = ScopeIds.Normalize(scopeId, nameof(scopeId));
        this.StaffMemberId = IntegrationEventContractGuards.RequireId(
            staffMemberId,
            nameof(staffMemberId));
        this.ContractVersion = contractVersion > 0
            ? contractVersion
            : throw new ArgumentOutOfRangeException(nameof(contractVersion));
        this.ProjectionRevision = projectionRevision > 0
            ? projectionRevision
            : throw new ArgumentOutOfRangeException(nameof(projectionRevision));
        this.IsRestricted = isRestricted;
    }

    public string ScopeId { get; }
    public Guid StaffMemberId { get; }
    public int ContractVersion { get; }
    public long ProjectionRevision { get; }
    public bool IsRestricted { get; }
    string IScopedIntegrationEvent.ScopeId => this.ScopeId;
}
