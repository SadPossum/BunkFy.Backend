namespace BunkFy.Modules.DataRights.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record DataRightsAnonymisationExecutionPreparedIntegrationEventV2
    : TenantIntegrationEvent
{
    public const string EventType =
        DataRightsAnonymisationExecutionPreparedIntegrationEvent.EventType;
    public const int EventVersion = 2;

    public DataRightsAnonymisationExecutionPreparedIntegrationEventV2(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid workItemId,
        Guid caseId,
        DataRightsCaseType caseType,
        DataRightsExecutionScopeKind scopeKind,
        Guid? propertyId,
        long approvalRevision,
        long executionRevision)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.WorkItemId = IntegrationEventContractGuards.RequireId(
            workItemId,
            nameof(workItemId));
        this.CaseId = IntegrationEventContractGuards.RequireId(caseId, nameof(caseId));
        (this.CaseType, this.ScopeKind, this.PropertyId) =
            RequireScope(caseType, scopeKind, propertyId);
        this.ApprovalRevision = approvalRevision > 0
            ? approvalRevision
            : throw new ArgumentOutOfRangeException(nameof(approvalRevision));
        this.ExecutionRevision = executionRevision > approvalRevision
            ? executionRevision
            : throw new ArgumentOutOfRangeException(nameof(executionRevision));
    }

    public Guid WorkItemId { get; }
    public Guid CaseId { get; }
    public DataRightsCaseType CaseType { get; }
    public DataRightsExecutionScopeKind ScopeKind { get; }
    public Guid? PropertyId { get; }
    public long ApprovalRevision { get; }
    public long ExecutionRevision { get; }

    private static (
        DataRightsCaseType CaseType,
        DataRightsExecutionScopeKind ScopeKind,
        Guid? PropertyId) RequireScope(
            DataRightsCaseType caseType,
            DataRightsExecutionScopeKind scopeKind,
            Guid? propertyId) =>
        (caseType, scopeKind, propertyId) switch
        {
            (DataRightsCaseType.GuestRights,
                DataRightsExecutionScopeKind.Property,
                Guid value) when value != Guid.Empty =>
                (caseType, scopeKind, value),
            (DataRightsCaseType.StaffRights,
                DataRightsExecutionScopeKind.Tenant,
                null) =>
                (caseType, scopeKind, null),
            _ => throw new ArgumentException(
                "The Data Rights execution scope is invalid.",
                nameof(scopeKind))
        };
}
