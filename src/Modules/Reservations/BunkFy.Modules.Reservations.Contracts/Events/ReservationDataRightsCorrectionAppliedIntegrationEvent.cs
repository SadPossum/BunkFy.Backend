namespace BunkFy.Modules.Reservations.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record ReservationDataRightsCorrectionAppliedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "reservation-data-rights-correction-applied";
    public const int EventVersion = 1;

    public ReservationDataRightsCorrectionAppliedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid receiptId,
        Guid propertyId,
        Guid caseId,
        long approvalRevision,
        Guid reservationId,
        long previousVersion,
        long currentVersion,
        long previousDetailsRevision,
        long currentDetailsRevision,
        IReadOnlyCollection<string> changedFields,
        Guid detailsChangeEventId)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.ReceiptId = IntegrationEventContractGuards.RequireId(receiptId, nameof(receiptId));
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.CaseId = IntegrationEventContractGuards.RequireId(caseId, nameof(caseId));
        this.ReservationId = IntegrationEventContractGuards.RequireId(
            reservationId,
            nameof(reservationId));
        this.DetailsChangeEventId = IntegrationEventContractGuards.RequireId(
            detailsChangeEventId,
            nameof(detailsChangeEventId));
        if (approvalRevision < 1 ||
            previousVersion < 1 ||
            currentVersion != previousVersion + 1 ||
            previousDetailsRevision < 0 ||
            currentDetailsRevision != previousDetailsRevision + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(currentVersion));
        }

        ArgumentNullException.ThrowIfNull(changedFields);
        string[] fields = changedFields
            .Where(field => !string.IsNullOrWhiteSpace(field))
            .Select(field => field.Trim())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (fields.Length == 0 ||
            fields.Any(field => !ReservationDataRightsFieldKeys.All.Contains(field)))
        {
            throw new ArgumentException(
                "At least one known reservation correction field is required.",
                nameof(changedFields));
        }

        this.ApprovalRevision = approvalRevision;
        this.PreviousVersion = previousVersion;
        this.CurrentVersion = currentVersion;
        this.PreviousDetailsRevision = previousDetailsRevision;
        this.CurrentDetailsRevision = currentDetailsRevision;
        this.ChangedFields = Array.AsReadOnly(fields);
    }

    public Guid ReceiptId { get; }
    public Guid PropertyId { get; }
    public Guid CaseId { get; }
    public long ApprovalRevision { get; }
    public Guid ReservationId { get; }
    public long PreviousVersion { get; }
    public long CurrentVersion { get; }
    public long PreviousDetailsRevision { get; }
    public long CurrentDetailsRevision { get; }
    public IReadOnlyCollection<string> ChangedFields { get; }
    public Guid DetailsChangeEventId { get; }
}
