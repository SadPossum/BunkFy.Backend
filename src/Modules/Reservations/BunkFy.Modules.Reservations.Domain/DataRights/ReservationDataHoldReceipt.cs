namespace BunkFy.Modules.Reservations.Domain.DataRights;

using BunkFy.Modules.Reservations.Domain.Errors;
using BunkFy.Modules.Reservations.Domain.Models;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class ReservationDataHoldReceipt : ScopedAggregateRoot<Guid>
{
    private ReservationDataHoldReceipt() { }

    private ReservationDataHoldReceipt(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public Guid IdempotencyKey { get; private set; }
    public Guid HoldId { get; private set; }
    public ReservationDataHoldAction Action { get; private set; }
    public Guid PropertyId { get; private set; }
    public Guid ReservationId { get; private set; }
    public string ReasonCode { get; private set; } = string.Empty;
    public long SelectedReservationVersion { get; private set; }
    public long SelectedDetailsRevision { get; private set; }
    public long ResultingHoldVersion { get; private set; }
    public DateTimeOffset CompletedAtUtc { get; private set; }

    public static Result<ReservationDataHoldReceipt> Create(
        Guid receiptId,
        string tenantId,
        Guid idempotencyKey,
        ReservationDataHold hold,
        ReservationDataHoldAction action,
        long selectedReservationVersion,
        long selectedDetailsRevision,
        DateTimeOffset completedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(hold);
        if (receiptId == Guid.Empty ||
            idempotencyKey == Guid.Empty ||
            hold.Id == Guid.Empty ||
            hold.PropertyId == Guid.Empty ||
            hold.ReservationId == Guid.Empty ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId) ||
            !string.Equals(scopeId, hold.ScopeId, StringComparison.Ordinal))
        {
            return Result.Failure<ReservationDataHoldReceipt>(
                ReservationsDomainErrors.DataHoldReceiptIdentityInvalid);
        }

        bool transitionValid = action switch
        {
            ReservationDataHoldAction.Place =>
                hold.State == ReservationDataHoldState.Active &&
                hold.Version == 1 &&
                completedAtUtc == hold.PlacedAtUtc,
            ReservationDataHoldAction.Release =>
                hold.State == ReservationDataHoldState.Released &&
                hold.Version == 2 &&
                completedAtUtc == hold.ReleasedAtUtc,
            _ => false
        };
        if (selectedReservationVersion < 1 ||
            selectedDetailsRevision < 1 ||
            !transitionValid)
        {
            return Result.Failure<ReservationDataHoldReceipt>(
                ReservationsDomainErrors.DataHoldReceiptVersionInvalid);
        }

        if (string.IsNullOrWhiteSpace(hold.ReasonCode) ||
            completedAtUtc == default)
        {
            return Result.Failure<ReservationDataHoldReceipt>(
                ReservationsDomainErrors.DataHoldReceiptLifecycleInvalid);
        }

        return Result.Success(new ReservationDataHoldReceipt(
            receiptId,
            scopeId)
        {
            IdempotencyKey = idempotencyKey,
            HoldId = hold.Id,
            Action = action,
            PropertyId = hold.PropertyId,
            ReservationId = hold.ReservationId,
            ReasonCode = hold.ReasonCode,
            SelectedReservationVersion = selectedReservationVersion,
            SelectedDetailsRevision = selectedDetailsRevision,
            ResultingHoldVersion = hold.Version,
            CompletedAtUtc = completedAtUtc
        });
    }
}
