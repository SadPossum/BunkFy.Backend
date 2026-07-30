namespace BunkFy.Modules.Workspaces.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Naming;
using Gma.Framework.Scoping;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[ScopeAware]
public sealed record
    WorkspaceStaffOnboardingProcessingRestrictionChangedIntegrationEvent
    : IntegrationEvent, IScopedIntegrationEvent
{
    public const string EventType =
        "workspace-staff-onboarding-processing-restriction-changed";
    public const int EventVersion = 1;

    public WorkspaceStaffOnboardingProcessingRestrictionChangedIntegrationEvent(
        Guid eventId,
        string scopeId,
        DateTimeOffset occurredAtUtc,
        Guid applicationId,
        int contractVersion,
        long projectionRevision,
        bool isRestricted)
        : base(eventId, occurredAtUtc, EventType, EventVersion)
    {
        this.ScopeId = ScopeIds.Normalize(scopeId, nameof(scopeId));
        this.ApplicationId = IntegrationEventContractGuards.RequireId(
            applicationId,
            nameof(applicationId));
        this.ContractVersion = contractVersion > 0
            ? contractVersion
            : throw new ArgumentOutOfRangeException(nameof(contractVersion));
        this.ProjectionRevision = projectionRevision > 0
            ? projectionRevision
            : throw new ArgumentOutOfRangeException(nameof(projectionRevision));
        this.IsRestricted = isRestricted;
    }

    public string ScopeId { get; }
    public Guid ApplicationId { get; }
    public int ContractVersion { get; }
    public long ProjectionRevision { get; }
    public bool IsRestricted { get; }
    string IScopedIntegrationEvent.ScopeId => this.ScopeId;
}
