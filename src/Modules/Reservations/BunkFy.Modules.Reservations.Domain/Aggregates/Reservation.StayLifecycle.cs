namespace BunkFy.Modules.Reservations.Domain.Aggregates;

using Gma.Framework.Results;
using BunkFy.Modules.Reservations.Domain.Entities;
using BunkFy.Modules.Reservations.Domain.Errors;
using BunkFy.Modules.Reservations.Domain.Events;

public sealed partial class Reservation
{
    public Result RequestExternalCancellation(
        long expectedDetailsRevision,
        Guid releaseRequestId,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        if (expectedDetailsRevision != this.DetailsRevision)
        {
            return Result.Failure(ReservationsDomainErrors.DetailsRevisionConflict);
        }

        return this.RequestCancellation(this.Version, releaseRequestId, eventId, nowUtc);
    }

    public Result CheckIn(
        long expectedVersion,
        DateOnly businessDate,
        string actorId,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        Result provenance = ValidateStayProvenance(actorId, eventId);
        if (provenance.IsFailure)
        {
            return provenance;
        }

        if (expectedVersion != this.Version)
        {
            return Result.Failure(ReservationsDomainErrors.VersionConflict);
        }

        if (this.PendingAllocationAmendmentId.HasValue)
        {
            return Result.Failure(ReservationsDomainErrors.AllocationAmendmentInProgress);
        }

        if (this.Status != ReservationState.Confirmed || this.AllocationId is null ||
            this.AllocationVersion is null)
        {
            return Result.Failure(ReservationsDomainErrors.InvalidTransition);
        }

        if (businessDate < this.Arrival || businessDate >= this.Departure)
        {
            return Result.Failure(ReservationsDomainErrors.StayBusinessDateInvalid);
        }

        string normalizedActor = actorId.Trim();
        this.Status = ReservationState.CheckedIn;
        this.CheckedInBusinessDate = businessDate;
        this.CheckedInAtUtc = nowUtc;
        this.CheckedInBy = normalizedActor;
        this.LastReleaseRejectionCode = null;
        this.Version++;
        this.UpdatedAtUtc = nowUtc;
        this.RaiseDomainEvent(new ReservationCheckedInDomainEvent(
            eventId,
            nowUtc,
            this.ScopeId,
            this.Id,
            this.PropertyId,
            businessDate,
            normalizedActor,
            this.Version));
        this.RaiseGuestStayChanged(eventId, nowUtc);
        return Result.Success();
    }

    public Result RequestNoShow(
        long expectedVersion,
        DateOnly businessDate,
        string actorId,
        Guid releaseRequestId,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        Result provenance = ValidateStayReleaseProvenance(actorId, releaseRequestId, eventId);
        if (provenance.IsFailure)
        {
            return provenance;
        }

        if (expectedVersion != this.Version)
        {
            return Result.Failure(ReservationsDomainErrors.VersionConflict);
        }

        if (this.PendingAllocationAmendmentId.HasValue)
        {
            return Result.Failure(ReservationsDomainErrors.AllocationAmendmentInProgress);
        }

        if (this.Status != ReservationState.Confirmed || this.AllocationId is null ||
            this.AllocationVersion is null)
        {
            return Result.Failure(ReservationsDomainErrors.InvalidTransition);
        }

        if (businessDate < this.Arrival)
        {
            return Result.Failure(ReservationsDomainErrors.StayBusinessDateInvalid);
        }

        string normalizedActor = actorId.Trim();
        this.Status = ReservationState.NoShowPending;
        this.ReleaseRequestId = releaseRequestId;
        this.PendingStayBusinessDate = businessDate;
        this.PendingStayActorId = normalizedActor;
        this.LastReleaseRejectionCode = null;
        this.Version++;
        this.UpdatedAtUtc = nowUtc;
        this.RaiseDomainEvent(new ReservationNoShowRequestedDomainEvent(
            eventId,
            nowUtc,
            this.ScopeId,
            this.Id,
            this.PropertyId,
            this.AllocationId.Value,
            releaseRequestId,
            this.AllocationVersion.Value));
        this.RaiseGuestStayChanged(eventId, nowUtc);
        return Result.Success();
    }

    public Result RequestCheckout(
        long expectedVersion,
        DateOnly businessDate,
        string actorId,
        Guid releaseRequestId,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        Result provenance = ValidateStayReleaseProvenance(actorId, releaseRequestId, eventId);
        if (provenance.IsFailure)
        {
            return provenance;
        }

        if (expectedVersion != this.Version)
        {
            return Result.Failure(ReservationsDomainErrors.VersionConflict);
        }

        if (this.Status != ReservationState.CheckedIn || this.AllocationId is null ||
            this.AllocationVersion is null || !this.CheckedInBusinessDate.HasValue)
        {
            return Result.Failure(ReservationsDomainErrors.InvalidTransition);
        }

        if (businessDate < this.CheckedInBusinessDate.Value)
        {
            return Result.Failure(ReservationsDomainErrors.StayBusinessDateInvalid);
        }

        string normalizedActor = actorId.Trim();
        this.Status = ReservationState.CheckoutPending;
        this.ReleaseRequestId = releaseRequestId;
        this.PendingStayBusinessDate = businessDate;
        this.PendingStayActorId = normalizedActor;
        this.LastReleaseRejectionCode = null;
        this.Version++;
        this.UpdatedAtUtc = nowUtc;
        this.RaiseDomainEvent(new ReservationCheckoutRequestedDomainEvent(
            eventId,
            nowUtc,
            this.ScopeId,
            this.Id,
            this.PropertyId,
            this.AllocationId.Value,
            releaseRequestId,
            this.AllocationVersion.Value));
        this.RaiseGuestStayChanged(eventId, nowUtc);
        return Result.Success();
    }

    public Result RequestCancellation(
        long expectedVersion,
        Guid releaseRequestId,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        if (this.PendingAllocationAmendmentId.HasValue)
        {
            return Result.Failure(ReservationsDomainErrors.AllocationAmendmentInProgress);
        }

        if (expectedVersion != this.Version)
        {
            return Result.Failure(ReservationsDomainErrors.VersionConflict);
        }

        if (this.Status == ReservationState.Cancelled)
        {
            return Result.Success();
        }

        if (this.Status == ReservationState.PendingAllocation)
        {
            this.Status = ReservationState.CancellationPending;
            this.ReleaseRequestId = releaseRequestId;
            this.LastReleaseRejectionCode = null;
            this.Version++;
            this.UpdatedAtUtc = nowUtc;
            this.RaiseGuestStayChanged(eventId, nowUtc);
            return Result.Success();
        }

        if (this.Status == ReservationState.AllocationRejected)
        {
            this.Status = ReservationState.Cancelled;
            this.Version++;
            this.UpdatedAtUtc = nowUtc;
            this.RaiseDomainEvent(new ReservationCancelledDomainEvent(
                eventId,
                nowUtc,
                this.ScopeId,
                this.Id,
                this.PropertyId,
                this.Version));
            this.RaiseGuestStayChanged(eventId, nowUtc);
            return Result.Success();
        }

        if (this.Status != ReservationState.Confirmed || this.AllocationId is null || this.AllocationVersion is null)
        {
            return Result.Failure(ReservationsDomainErrors.InvalidTransition);
        }

        this.Status = ReservationState.CancellationPending;
        this.ReleaseRequestId = releaseRequestId;
        this.LastReleaseRejectionCode = null;
        this.Version++;
        this.UpdatedAtUtc = nowUtc;
        this.RaiseDomainEvent(new ReservationCancellationRequestedDomainEvent(
            eventId,
            nowUtc,
            this.ScopeId,
            this.Id,
            this.PropertyId,
            this.AllocationId.Value,
            releaseRequestId,
            this.AllocationVersion.Value));
        this.RaiseGuestStayChanged(eventId, nowUtc);
        return Result.Success();
    }

    public Result<ReservationReleaseCompletion> CompleteAllocationRelease(
        Guid releaseRequestId,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        ReservationReleaseCompletion terminal = this.Status switch
        {
            ReservationState.Cancelled => ReservationReleaseCompletion.Cancelled,
            ReservationState.NoShow => ReservationReleaseCompletion.NoShow,
            ReservationState.CheckedOut => ReservationReleaseCompletion.CheckedOut,
            _ => ReservationReleaseCompletion.Unknown
        };
        if (terminal != ReservationReleaseCompletion.Unknown)
        {
            return this.ReleaseRequestId == releaseRequestId
                ? Result.Success(terminal)
                : Result.Failure<ReservationReleaseCompletion>(
                    ReservationsDomainErrors.AllocationCorrelationMismatch);
        }

        if (releaseRequestId == Guid.Empty || eventId == Guid.Empty || this.ReleaseRequestId != releaseRequestId)
        {
            return Result.Failure<ReservationReleaseCompletion>(
                ReservationsDomainErrors.AllocationCorrelationMismatch);
        }

        ReservationReleaseCompletion completion;
        switch (this.Status)
        {
            case ReservationState.CancellationPending:
                this.Status = ReservationState.Cancelled;
                completion = ReservationReleaseCompletion.Cancelled;
                break;
            case ReservationState.NoShowPending when
                this.PendingStayBusinessDate.HasValue && !string.IsNullOrWhiteSpace(this.PendingStayActorId):
                this.Status = ReservationState.NoShow;
                this.NoShowBusinessDate = this.PendingStayBusinessDate;
                this.NoShowAtUtc = nowUtc;
                this.NoShowBy = this.PendingStayActorId;
                completion = ReservationReleaseCompletion.NoShow;
                break;
            case ReservationState.CheckoutPending when
                this.PendingStayBusinessDate.HasValue && !string.IsNullOrWhiteSpace(this.PendingStayActorId):
                this.Status = ReservationState.CheckedOut;
                this.CheckedOutBusinessDate = this.PendingStayBusinessDate;
                this.CheckedOutAtUtc = nowUtc;
                this.CheckedOutBy = this.PendingStayActorId;
                completion = ReservationReleaseCompletion.CheckedOut;
                break;
            case ReservationState.PendingAllocation:
            case ReservationState.Confirmed:
            case ReservationState.AllocationRejected:
            case ReservationState.Cancelled:
            case ReservationState.CheckedIn:
            case ReservationState.NoShowPending:
            case ReservationState.NoShow:
            case ReservationState.CheckoutPending:
            case ReservationState.CheckedOut:
            default:
                return Result.Failure<ReservationReleaseCompletion>(
                    ReservationsDomainErrors.AllocationCorrelationMismatch);
        }

        this.PendingStayBusinessDate = null;
        this.PendingStayActorId = null;
        this.LastReleaseRejectionCode = null;
        this.Version++;
        this.UpdatedAtUtc = nowUtc;
        switch (completion)
        {
            case ReservationReleaseCompletion.Cancelled:
                this.RaiseDomainEvent(new ReservationCancelledDomainEvent(
                    eventId, nowUtc, this.ScopeId, this.Id, this.PropertyId, this.Version));
                break;
            case ReservationReleaseCompletion.NoShow:
                this.RaiseDomainEvent(new ReservationNoShowDomainEvent(
                    eventId,
                    nowUtc,
                    this.ScopeId,
                    this.Id,
                    this.PropertyId,
                    this.NoShowBusinessDate!.Value,
                    this.NoShowBy!,
                    this.Version));
                break;
            case ReservationReleaseCompletion.CheckedOut:
                this.RaiseDomainEvent(new ReservationCheckedOutDomainEvent(
                    eventId,
                    nowUtc,
                    this.ScopeId,
                    this.Id,
                    this.PropertyId,
                    this.CheckedOutBusinessDate!.Value,
                    this.CheckedOutBy!,
                    this.Version));
                break;
            case ReservationReleaseCompletion.Unknown:
            default:
                throw new InvalidOperationException("A release completion must be terminal.");
        }

        this.RaiseGuestStayChanged(eventId, nowUtc);

        return Result.Success(completion);
    }

    public Result RestoreAfterReleaseRejection(
        Guid releaseRequestId,
        int rejectionCode,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        if (this.ReleaseRequestId != releaseRequestId)
        {
            return Result.Failure(ReservationsDomainErrors.AllocationCorrelationMismatch);
        }

        this.Status = this.Status switch
        {
            ReservationState.CancellationPending => ReservationState.Confirmed,
            ReservationState.NoShowPending => ReservationState.Confirmed,
            ReservationState.CheckoutPending => ReservationState.CheckedIn,
            _ => this.Status
        };
        if (this.Status is not (ReservationState.Confirmed or ReservationState.CheckedIn))
        {
            return Result.Failure(ReservationsDomainErrors.AllocationCorrelationMismatch);
        }

        this.ReleaseRequestId = null;
        this.PendingStayBusinessDate = null;
        this.PendingStayActorId = null;
        this.LastReleaseRejectionCode = rejectionCode;
        this.Version++;
        this.UpdatedAtUtc = nowUtc;
        this.RaiseGuestStayChanged(eventId, nowUtc);
        return Result.Success();
    }

}
