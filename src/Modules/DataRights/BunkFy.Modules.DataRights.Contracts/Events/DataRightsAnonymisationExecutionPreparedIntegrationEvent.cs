namespace BunkFy.Modules.DataRights.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record DataRightsAnonymisationExecutionPreparedIntegrationEvent
    : TenantIntegrationEvent
{
    public const string EventType = "data-rights-anonymisation-execution-prepared";
    public const int EventVersion = 1;

    public DataRightsAnonymisationExecutionPreparedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid workItemId,
        Guid caseId,
        Guid propertyId,
        long approvalRevision,
        long executionRevision)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.WorkItemId = IntegrationEventContractGuards.RequireId(
            workItemId,
            nameof(workItemId));
        this.CaseId = IntegrationEventContractGuards.RequireId(caseId, nameof(caseId));
        this.PropertyId = IntegrationEventContractGuards.RequireId(
            propertyId,
            nameof(propertyId));
        this.ApprovalRevision = approvalRevision > 0
            ? approvalRevision
            : throw new ArgumentOutOfRangeException(nameof(approvalRevision));
        this.ExecutionRevision = executionRevision > approvalRevision
            ? executionRevision
            : throw new ArgumentOutOfRangeException(nameof(executionRevision));
    }

    public Guid WorkItemId { get; }
    public Guid CaseId { get; }
    public Guid PropertyId { get; }
    public long ApprovalRevision { get; }
    public long ExecutionRevision { get; }
}
