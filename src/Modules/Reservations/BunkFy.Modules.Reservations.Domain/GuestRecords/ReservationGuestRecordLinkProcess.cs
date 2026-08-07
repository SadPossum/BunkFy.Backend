namespace BunkFy.Modules.Reservations.Domain.GuestRecords;

using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.Errors;
using BunkFy.Modules.Reservations.Domain.Events;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class ReservationGuestRecordLinkProcess : ScopedAggregateRoot<Guid>
{
    private ReservationGuestRecordLinkProcess() { }

    private ReservationGuestRecordLinkProcess(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public Guid PropertyId { get; private set; }
    public Guid ReservationId { get; private set; }
    public Guid CreationConfirmationId { get; private set; }
    public long ExpectedReservationVersion { get; private set; }
    public string? RequestedBy { get; private set; }
    public ReservationGuestRecordLinkProcessState State { get; private set; }
    public ReservationGuestRecordLinkReviewReason ReviewReason { get; private set; }
    public long Revision { get; private set; }
    public int DispatchRevision { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Result<ReservationGuestRecordLinkProcess> Prepare(
        Guid operationId,
        string tenantId,
        Guid propertyId,
        Guid reservationId,
        Guid creationConfirmationId,
        long expectedReservationVersion,
        string actorId,
        DateTimeOffset nowUtc)
    {
        if (operationId == Guid.Empty ||
            propertyId == Guid.Empty ||
            reservationId == Guid.Empty ||
            creationConfirmationId == Guid.Empty ||
            expectedReservationVersion <= 0 ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Result.Failure<ReservationGuestRecordLinkProcess>(
                ReservationsDomainErrors.GuestRecordLinkProcessIdentityInvalid);
        }

        string normalizedActor = actorId?.Trim() ?? string.Empty;
        if (normalizedActor.Length is 0 or > Reservation.ActorIdMaxLength)
        {
            return Result.Failure<ReservationGuestRecordLinkProcess>(
                ReservationsDomainErrors.GuestRecordLinkProcessActorInvalid);
        }

        if (nowUtc == default)
        {
            return Result.Failure<ReservationGuestRecordLinkProcess>(
                ReservationsDomainErrors.GuestRecordLinkProcessLifecycleInvalid);
        }

        return Result.Success(new ReservationGuestRecordLinkProcess(operationId, scopeId)
        {
            PropertyId = propertyId,
            ReservationId = reservationId,
            CreationConfirmationId = creationConfirmationId,
            ExpectedReservationVersion = expectedReservationVersion,
            RequestedBy = normalizedActor,
            State = ReservationGuestRecordLinkProcessState.Prepared,
            ReviewReason = ReservationGuestRecordLinkReviewReason.None,
            Revision = 1,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        });
    }

    public bool MatchesPreparation(
        Guid propertyId,
        Guid reservationId,
        long expectedReservationVersion) =>
        this.PropertyId == propertyId &&
        this.ReservationId == reservationId &&
        this.ExpectedReservationVersion == expectedReservationVersion;

    public Result<bool> ConfirmGuest(
        Guid creationConfirmationId,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        if (creationConfirmationId == Guid.Empty ||
            creationConfirmationId != this.CreationConfirmationId)
        {
            return Result.Failure<bool>(
                ReservationsDomainErrors.GuestRecordLinkProcessCorrelationMismatch);
        }

        if (this.State is ReservationGuestRecordLinkProcessState.Ready or
            ReservationGuestRecordLinkProcessState.Completed or
            ReservationGuestRecordLinkProcessState.NeedsReview)
        {
            return Result.Success(false);
        }

        if (this.State != ReservationGuestRecordLinkProcessState.Prepared ||
            eventId == Guid.Empty ||
            !this.IsValidTimestamp(nowUtc))
        {
            return Result.Failure<bool>(
                ReservationsDomainErrors.GuestRecordLinkProcessTransitionInvalid);
        }

        this.State = ReservationGuestRecordLinkProcessState.Ready;
        this.DispatchRevision = 1;
        this.Advance(nowUtc);
        this.RaiseReady(eventId, nowUtc);
        return Result.Success(true);
    }

    public Result<bool> Redispatch(Guid eventId, DateTimeOffset nowUtc)
    {
        if (this.State == ReservationGuestRecordLinkProcessState.Completed)
        {
            return Result.Success(false);
        }

        if (this.State is not (
                ReservationGuestRecordLinkProcessState.Ready or
                ReservationGuestRecordLinkProcessState.NeedsReview) ||
            eventId == Guid.Empty ||
            this.DispatchRevision == int.MaxValue ||
            !this.IsValidTimestamp(nowUtc))
        {
            return Result.Failure<bool>(
                ReservationsDomainErrors.GuestRecordLinkProcessTransitionInvalid);
        }

        this.State = ReservationGuestRecordLinkProcessState.Ready;
        this.ReviewReason = ReservationGuestRecordLinkReviewReason.None;
        this.DispatchRevision++;
        this.Advance(nowUtc);
        this.RaiseReady(eventId, nowUtc);
        return Result.Success(true);
    }

    public Result<bool> Complete(DateTimeOffset nowUtc)
    {
        if (this.State == ReservationGuestRecordLinkProcessState.Completed)
        {
            return Result.Success(false);
        }

        if (this.State != ReservationGuestRecordLinkProcessState.Ready ||
            !this.IsValidTimestamp(nowUtc))
        {
            return Result.Failure<bool>(
                ReservationsDomainErrors.GuestRecordLinkProcessTransitionInvalid);
        }

        this.State = ReservationGuestRecordLinkProcessState.Completed;
        this.ReviewReason = ReservationGuestRecordLinkReviewReason.None;
        this.RequestedBy = null;
        this.Advance(nowUtc);
        return Result.Success(true);
    }

    public Result<bool> RequireReview(
        ReservationGuestRecordLinkReviewReason reason,
        DateTimeOffset nowUtc)
    {
        if (reason is ReservationGuestRecordLinkReviewReason.Unknown or
            ReservationGuestRecordLinkReviewReason.None)
        {
            return Result.Failure<bool>(
                ReservationsDomainErrors.GuestRecordLinkProcessReviewReasonInvalid);
        }

        if (this.State == ReservationGuestRecordLinkProcessState.NeedsReview &&
            this.ReviewReason == reason)
        {
            return Result.Success(false);
        }

        if (this.State != ReservationGuestRecordLinkProcessState.Ready ||
            !this.IsValidTimestamp(nowUtc))
        {
            return Result.Failure<bool>(
                ReservationsDomainErrors.GuestRecordLinkProcessTransitionInvalid);
        }

        this.State = ReservationGuestRecordLinkProcessState.NeedsReview;
        this.ReviewReason = reason;
        this.Advance(nowUtc);
        return Result.Success(true);
    }

    public Result<bool> TerminateForGuestAnonymisation(DateTimeOffset nowUtc)
    {
        if (this.State == ReservationGuestRecordLinkProcessState.Completed ||
            (this.State == ReservationGuestRecordLinkProcessState.NeedsReview &&
             this.ReviewReason ==
                ReservationGuestRecordLinkReviewReason.GuestUnavailable))
        {
            return Result.Success(false);
        }

        if (this.State is not (
                ReservationGuestRecordLinkProcessState.Prepared or
                ReservationGuestRecordLinkProcessState.Ready or
                ReservationGuestRecordLinkProcessState.NeedsReview) ||
            !this.IsValidTimestamp(nowUtc))
        {
            return Result.Failure<bool>(
                ReservationsDomainErrors.GuestRecordLinkProcessTransitionInvalid);
        }

        this.State = ReservationGuestRecordLinkProcessState.NeedsReview;
        this.ReviewReason =
            ReservationGuestRecordLinkReviewReason.GuestUnavailable;
        this.Advance(nowUtc);
        return Result.Success(true);
    }

    private bool IsValidTimestamp(DateTimeOffset nowUtc) =>
        nowUtc != default && nowUtc >= this.CreatedAtUtc && nowUtc >= this.UpdatedAtUtc;

    private void Advance(DateTimeOffset nowUtc)
    {
        this.Revision++;
        this.UpdatedAtUtc = nowUtc;
    }

    private void RaiseReady(Guid eventId, DateTimeOffset nowUtc) =>
        this.RaiseDomainEvent(new ReservationGuestRecordLinkReadyDomainEvent(
            eventId,
            nowUtc,
            this.ScopeId,
            this.Id,
            this.PropertyId,
            this.ReservationId,
            this.DispatchRevision));
}
