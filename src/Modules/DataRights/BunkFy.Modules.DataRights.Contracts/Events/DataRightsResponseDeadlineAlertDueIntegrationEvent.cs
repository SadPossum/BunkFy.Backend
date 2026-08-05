namespace BunkFy.Modules.DataRights.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

public enum DataRightsResponseDeadlineAlertKind
{
    Unknown = 0,
    DueSoon = 1,
    Overdue = 2
}

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record DataRightsResponseDeadlineAlertDueIntegrationEvent
    : TenantIntegrationEvent
{
    public const string EventType = "data-rights-response-deadline-alert-due";
    public const int EventVersion = 1;

    public DataRightsResponseDeadlineAlertDueIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid caseId,
        Guid propertyId,
        DataRightsResponseDeadlineAlertKind alertKind,
        DateTimeOffset dueAtUtc)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.CaseId = IntegrationEventContractGuards.RequireId(
            caseId,
            nameof(caseId));
        this.PropertyId = IntegrationEventContractGuards.RequireId(
            propertyId,
            nameof(propertyId));
        this.AlertKind = alertKind is
            DataRightsResponseDeadlineAlertKind.DueSoon or
            DataRightsResponseDeadlineAlertKind.Overdue
                ? alertKind
                : throw new ArgumentOutOfRangeException(nameof(alertKind));
        this.DueAtUtc = dueAtUtc != default
            ? dueAtUtc
            : throw new ArgumentOutOfRangeException(nameof(dueAtUtc));
    }

    public Guid CaseId { get; }
    public Guid PropertyId { get; }
    public DataRightsResponseDeadlineAlertKind AlertKind { get; }
    public DateTimeOffset DueAtUtc { get; }
}
