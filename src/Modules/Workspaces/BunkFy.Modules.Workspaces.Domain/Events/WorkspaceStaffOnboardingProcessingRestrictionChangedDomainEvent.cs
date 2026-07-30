namespace BunkFy.Modules.Workspaces.Domain.Events;

using Gma.Framework.Domain;

public sealed record
    WorkspaceStaffOnboardingProcessingRestrictionChangedDomainEvent
    : ScopedDomainEvent
{
    public WorkspaceStaffOnboardingProcessingRestrictionChangedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string scopeId,
        Guid applicationId,
        int contractVersion,
        long projectionRevision,
        bool isRestricted)
        : base(eventId, occurredAtUtc, scopeId)
    {
        this.ApplicationId = applicationId;
        this.ContractVersion = contractVersion;
        this.ProjectionRevision = projectionRevision;
        this.IsRestricted = isRestricted;
    }

    public Guid ApplicationId { get; }
    public int ContractVersion { get; }
    public long ProjectionRevision { get; }
    public bool IsRestricted { get; }
}
