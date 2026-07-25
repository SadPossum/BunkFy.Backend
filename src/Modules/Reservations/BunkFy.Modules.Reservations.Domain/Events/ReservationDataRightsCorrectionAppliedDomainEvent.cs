namespace BunkFy.Modules.Reservations.Domain.Events;

using BunkFy.Modules.Reservations.Domain.Aggregates;
using Gma.Framework.Domain;

public sealed record ReservationDataRightsCorrectionAppliedDomainEvent : ScopedDomainEvent
{
    public ReservationDataRightsCorrectionAppliedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string scopeId,
        Guid receiptId,
        Guid propertyId,
        Guid caseId,
        long approvalRevision,
        Guid reservationId,
        long previousRecordVersion,
        long currentRecordVersion,
        long previousDetailsRevision,
        long currentDetailsRevision,
        IReadOnlyCollection<ReservationDetailsField> changedFields,
        Guid detailsChangeEventId)
        : base(eventId, occurredAtUtc, scopeId)
    {
        this.ReceiptId = DomainEventGuards.RequireId(receiptId, nameof(receiptId));
        this.PropertyId = DomainEventGuards.RequireId(propertyId, nameof(propertyId));
        this.CaseId = DomainEventGuards.RequireId(caseId, nameof(caseId));
        this.ReservationId = DomainEventGuards.RequireId(reservationId, nameof(reservationId));
        this.DetailsChangeEventId = DomainEventGuards.RequireId(
            detailsChangeEventId,
            nameof(detailsChangeEventId));
        if (approvalRevision < 1 ||
            previousRecordVersion < 1 ||
            currentRecordVersion != previousRecordVersion + 1 ||
            previousDetailsRevision < 0 ||
            currentDetailsRevision != previousDetailsRevision + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(currentRecordVersion));
        }

        ArgumentNullException.ThrowIfNull(changedFields);
        ReservationDetailsField[] fields = changedFields
            .Distinct()
            .Order()
            .ToArray();
        if (fields.Length == 0 || fields.Any(field =>
                field is ReservationDetailsField.Unknown || !Enum.IsDefined(field)))
        {
            throw new ArgumentException(
                "At least one correctable reservation field is required.",
                nameof(changedFields));
        }

        this.ApprovalRevision = approvalRevision;
        this.PreviousRecordVersion = previousRecordVersion;
        this.CurrentRecordVersion = currentRecordVersion;
        this.PreviousDetailsRevision = previousDetailsRevision;
        this.CurrentDetailsRevision = currentDetailsRevision;
        this.ChangedFields = Array.AsReadOnly(fields);
    }

    public Guid ReceiptId { get; }
    public Guid PropertyId { get; }
    public Guid CaseId { get; }
    public long ApprovalRevision { get; }
    public Guid ReservationId { get; }
    public long PreviousRecordVersion { get; }
    public long CurrentRecordVersion { get; }
    public long PreviousDetailsRevision { get; }
    public long CurrentDetailsRevision { get; }
    public IReadOnlyCollection<ReservationDetailsField> ChangedFields { get; }
    public Guid DetailsChangeEventId { get; }
}
