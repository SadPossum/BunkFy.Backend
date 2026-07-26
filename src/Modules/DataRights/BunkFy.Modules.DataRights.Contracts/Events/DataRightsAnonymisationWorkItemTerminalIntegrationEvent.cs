namespace BunkFy.Modules.DataRights.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record DataRightsAnonymisationWorkItemTerminalIntegrationEvent
    : TenantIntegrationEvent
{
    public const string EventType = "data-rights-anonymisation-work-item-terminal";
    public const int EventVersion = 1;

    public DataRightsAnonymisationWorkItemTerminalIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid batchId,
        Guid workItemId,
        Guid caseId,
        Guid propertyId,
        long executionRevision)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.BatchId = IntegrationEventContractGuards.RequireId(batchId, nameof(batchId));
        this.WorkItemId = IntegrationEventContractGuards.RequireId(
            workItemId,
            nameof(workItemId));
        this.CaseId = IntegrationEventContractGuards.RequireId(caseId, nameof(caseId));
        this.PropertyId = IntegrationEventContractGuards.RequireId(
            propertyId,
            nameof(propertyId));
        this.ExecutionRevision = executionRevision > 0
            ? executionRevision
            : throw new ArgumentOutOfRangeException(nameof(executionRevision));
    }

    public Guid BatchId { get; }
    public Guid WorkItemId { get; }
    public Guid CaseId { get; }
    public Guid PropertyId { get; }
    public long ExecutionRevision { get; }
}
