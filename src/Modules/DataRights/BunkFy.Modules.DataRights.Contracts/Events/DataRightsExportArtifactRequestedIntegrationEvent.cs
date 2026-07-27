namespace BunkFy.Modules.DataRights.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record DataRightsExportArtifactRequestedIntegrationEvent
    : TenantIntegrationEvent
{
    public const string EventType = "data-rights-export-artifact-requested";
    public const int EventVersion = 1;

    public DataRightsExportArtifactRequestedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid artifactId,
        Guid caseId,
        DataRightsCaseType caseType,
        Guid? propertyId,
        long decisionRevision,
        DateTimeOffset expiresAtUtc)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.ArtifactId = IntegrationEventContractGuards.RequireId(
            artifactId,
            nameof(artifactId));
        this.CaseId = IntegrationEventContractGuards.RequireId(caseId, nameof(caseId));
        this.CaseType = caseType is DataRightsCaseType.GuestRights or
            DataRightsCaseType.StaffRights
            ? caseType
            : throw new ArgumentOutOfRangeException(nameof(caseType));
        this.PropertyId = caseType switch
        {
            DataRightsCaseType.GuestRights when propertyId is not null &&
                propertyId != Guid.Empty => propertyId,
            DataRightsCaseType.StaffRights when propertyId is null => null,
            _ => throw new ArgumentException(
                "The data-rights export scope is invalid.",
                nameof(propertyId))
        };
        this.DecisionRevision = decisionRevision > 0
            ? decisionRevision
            : throw new ArgumentOutOfRangeException(nameof(decisionRevision));
        this.ExpiresAtUtc = expiresAtUtc > occurredAtUtc
            ? expiresAtUtc
            : throw new ArgumentOutOfRangeException(nameof(expiresAtUtc));
    }

    public Guid ArtifactId { get; }
    public Guid CaseId { get; }
    public DataRightsCaseType CaseType { get; }
    public Guid? PropertyId { get; }
    public long DecisionRevision { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
}
