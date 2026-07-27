namespace BunkFy.Modules.Reservations.Domain.DataRights;

using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.Errors;
using BunkFy.Modules.Reservations.Domain.Events;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class ReservationDataRightsCorrectionReceipt : ScopedAggregateRoot<Guid>
{
    public const int CurrentContractVersion = 1;
    public const int AllChangedFieldsMask = (1 << 7) - 1;

    private ReservationDataRightsCorrectionReceipt() { }

    private ReservationDataRightsCorrectionReceipt(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public int ContractVersion { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public Guid PropertyId { get; private set; }
    public Guid CaseId { get; private set; }
    public long ApprovalRevision { get; private set; }
    public Guid ReservationId { get; private set; }
    public long SelectedRecordVersion { get; private set; }
    public long CurrentRecordVersion { get; private set; }
    public long SelectedDetailsRevision { get; private set; }
    public long CurrentDetailsRevision { get; private set; }
    public int ChangedFieldsMask { get; private set; }
    public Guid DetailsChangeEventId { get; private set; }
    public Guid CorrelationId { get; private set; }
    public Guid EventId { get; private set; }
    public Guid CompletionEventId { get; private set; }
    public DateTimeOffset CompletedAtUtc { get; private set; }

    public IReadOnlyCollection<ReservationDetailsField> ChangedFields =>
        Enum.GetValues<ReservationDetailsField>()
            .Where(candidate => candidate is not ReservationDetailsField.Unknown &&
                (this.ChangedFieldsMask & ToMask(candidate)) != 0)
            .ToArray();

    public static Result<ReservationDataRightsCorrectionReceipt> Create(
        Guid receiptId,
        string tenantId,
        Guid idempotencyKey,
        Guid propertyId,
        Guid caseId,
        long approvalRevision,
        Guid reservationId,
        ReservationDataRightsCorrectionOutcome correction,
        Guid eventId,
        Guid completionEventId)
    {
        ArgumentNullException.ThrowIfNull(correction);

        if (receiptId == Guid.Empty ||
            idempotencyKey == Guid.Empty ||
            propertyId == Guid.Empty ||
            caseId == Guid.Empty ||
            reservationId == Guid.Empty ||
            correction.DetailsChangeEventId == Guid.Empty ||
            correction.CorrelationId == Guid.Empty ||
            eventId == Guid.Empty ||
            completionEventId == Guid.Empty ||
            completionEventId == eventId ||
            correction.CompletedAtUtc == default ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Result.Failure<ReservationDataRightsCorrectionReceipt>(
                ReservationsDomainErrors.DataRightsCorrectionReceiptIdentityInvalid);
        }

        if (approvalRevision < 1 ||
            correction.PreviousRecordVersion < 1 ||
            correction.CurrentRecordVersion != correction.PreviousRecordVersion + 1 ||
            correction.PreviousDetailsRevision < 0 ||
            correction.CurrentDetailsRevision != correction.PreviousDetailsRevision + 1)
        {
            return Result.Failure<ReservationDataRightsCorrectionReceipt>(
                ReservationsDomainErrors.DataRightsCorrectionReceiptVersionInvalid);
        }

        int changedFieldsMask = ToMask(correction.ChangedFields);
        if (changedFieldsMask is <= 0 or > AllChangedFieldsMask)
        {
            return Result.Failure<ReservationDataRightsCorrectionReceipt>(
                ReservationsDomainErrors.DataRightsCorrectionReceiptFieldsInvalid);
        }

        ReservationDataRightsCorrectionReceipt receipt = new(receiptId, scopeId)
        {
            ContractVersion = CurrentContractVersion,
            IdempotencyKey = idempotencyKey,
            PropertyId = propertyId,
            CaseId = caseId,
            ApprovalRevision = approvalRevision,
            ReservationId = reservationId,
            SelectedRecordVersion = correction.PreviousRecordVersion,
            CurrentRecordVersion = correction.CurrentRecordVersion,
            SelectedDetailsRevision = correction.PreviousDetailsRevision,
            CurrentDetailsRevision = correction.CurrentDetailsRevision,
            ChangedFieldsMask = changedFieldsMask,
            DetailsChangeEventId = correction.DetailsChangeEventId,
            CorrelationId = correction.CorrelationId,
            EventId = eventId,
            CompletionEventId = completionEventId,
            CompletedAtUtc = correction.CompletedAtUtc
        };
        receipt.RaiseDomainEvent(new ReservationDataRightsCorrectionAppliedDomainEvent(
            eventId,
            correction.CompletedAtUtc,
            scopeId,
            receiptId,
            propertyId,
            caseId,
            approvalRevision,
            reservationId,
            correction.PreviousRecordVersion,
            correction.CurrentRecordVersion,
            correction.PreviousDetailsRevision,
            correction.CurrentDetailsRevision,
            correction.ChangedFields,
            correction.DetailsChangeEventId,
            idempotencyKey,
            completionEventId));
        return Result.Success(receipt);
    }

    private static int ToMask(IReadOnlyCollection<ReservationDetailsField> fields)
    {
        if (fields is null || fields.Count == 0 || fields.Any(candidate =>
                candidate is ReservationDetailsField.Unknown || !Enum.IsDefined(candidate)))
        {
            return 0;
        }

        return fields.Aggregate(0, (mask, candidate) => mask | ToMask(candidate));
    }

    private static int ToMask(ReservationDetailsField field) => 1 << ((int)field - 1);
}
