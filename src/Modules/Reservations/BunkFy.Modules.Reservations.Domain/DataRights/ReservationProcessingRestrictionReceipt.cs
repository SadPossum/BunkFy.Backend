namespace BunkFy.Modules.Reservations.Domain.DataRights;

using BunkFy.Modules.Reservations.Domain.Errors;
using BunkFy.Modules.Reservations.Domain.Events;
using BunkFy.Modules.Reservations.Domain.Models;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class ReservationProcessingRestrictionReceipt : ScopedAggregateRoot<Guid>
{
    private ReservationProcessingRestrictionReceipt() { }

    private ReservationProcessingRestrictionReceipt(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public Guid IdempotencyKey { get; private set; }
    public Guid RestrictionId { get; private set; }
    public ReservationProcessingRestrictionAction Action { get; private set; }
    public Guid PropertyId { get; private set; }
    public Guid ReservationId { get; private set; }
    public Guid CaseId { get; private set; }
    public long ApprovalRevision { get; private set; }
    public long SelectedReservationVersion { get; private set; }
    public int ContractVersion { get; private set; }
    public long ResultingRestrictionVersion { get; private set; }
    public long ResultingProjectionRevision { get; private set; }
    public bool EffectiveRestricted { get; private set; }
    public Guid EventId { get; private set; }
    public DateTimeOffset CompletedAtUtc { get; private set; }

    public static Result<ReservationProcessingRestrictionReceipt> Create(
        Guid receiptId,
        string tenantId,
        Guid idempotencyKey,
        Guid restrictionId,
        ReservationProcessingRestrictionAction action,
        Guid propertyId,
        Guid reservationId,
        Guid caseId,
        long approvalRevision,
        long selectedReservationVersion,
        int contractVersion,
        long resultingRestrictionVersion,
        long resultingProjectionRevision,
        bool effectiveRestricted,
        Guid eventId,
        DateTimeOffset completedAtUtc)
    {
        if (receiptId == Guid.Empty ||
            idempotencyKey == Guid.Empty ||
            restrictionId == Guid.Empty ||
            propertyId == Guid.Empty ||
            reservationId == Guid.Empty ||
            caseId == Guid.Empty ||
            eventId == Guid.Empty ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Result.Failure<ReservationProcessingRestrictionReceipt>(
                ReservationsDomainErrors.ProcessingRestrictionReceiptIdentityInvalid);
        }

        bool restrictionVersionValid = action switch
        {
            ReservationProcessingRestrictionAction.Apply =>
                resultingRestrictionVersion == 1,
            ReservationProcessingRestrictionAction.Release =>
                resultingRestrictionVersion >= 2,
            _ => false
        };
        if (approvalRevision < 1 ||
            selectedReservationVersion < 1 ||
            contractVersion < 1 ||
            resultingProjectionRevision < 1 ||
            !restrictionVersionValid)
        {
            return Result.Failure<ReservationProcessingRestrictionReceipt>(
                ReservationsDomainErrors.ProcessingRestrictionReceiptVersionInvalid);
        }

        if (completedAtUtc == default ||
            (action == ReservationProcessingRestrictionAction.Apply &&
             !effectiveRestricted))
        {
            return Result.Failure<ReservationProcessingRestrictionReceipt>(
                ReservationsDomainErrors.ProcessingRestrictionReceiptTransitionInvalid);
        }

        ReservationProcessingRestrictionReceipt receipt = new(receiptId, scopeId)
        {
            IdempotencyKey = idempotencyKey,
            RestrictionId = restrictionId,
            Action = action,
            PropertyId = propertyId,
            ReservationId = reservationId,
            CaseId = caseId,
            ApprovalRevision = approvalRevision,
            SelectedReservationVersion = selectedReservationVersion,
            ContractVersion = contractVersion,
            ResultingRestrictionVersion = resultingRestrictionVersion,
            ResultingProjectionRevision = resultingProjectionRevision,
            EffectiveRestricted = effectiveRestricted,
            EventId = eventId,
            CompletedAtUtc = completedAtUtc
        };
        receipt.RaiseDomainEvent(new ReservationProcessingRestrictionChangedDomainEvent(
            eventId,
            completedAtUtc,
            scopeId,
            propertyId,
            reservationId,
            contractVersion,
            resultingProjectionRevision,
            effectiveRestricted));
        return Result.Success(receipt);
    }
}
