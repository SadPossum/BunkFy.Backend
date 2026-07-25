namespace BunkFy.Modules.Reservations.Domain.Aggregates;

public sealed record ReservationDataRightsCorrectionOutcome(
    long PreviousRecordVersion,
    long CurrentRecordVersion,
    long PreviousDetailsRevision,
    long CurrentDetailsRevision,
    IReadOnlyCollection<ReservationDetailsField> ChangedFields,
    Guid DetailsChangeEventId,
    Guid CorrelationId,
    DateTimeOffset CompletedAtUtc);
