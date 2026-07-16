namespace BunkFy.Modules.Reservations.Domain.Aggregates;

using Gma.Framework.Results;
using BunkFy.Modules.Reservations.Domain.Entities;
using BunkFy.Modules.Reservations.Domain.Errors;
using BunkFy.Modules.Reservations.Domain.Events;

public sealed partial class Reservation
{
    public Result ConfirmAllocation(
        Guid allocationRequestId,
        Guid allocationId,
        long allocationVersion,
        Guid cancellationEventId,
        DateTimeOffset nowUtc)
    {
        if (allocationRequestId != this.AllocationRequestId)
        {
            return Result.Failure(ReservationsDomainErrors.AllocationCorrelationMismatch);
        }

        if (this.Status == ReservationState.Confirmed && this.AllocationId == allocationId)
        {
            return Result.Success();
        }

        if (this.Status == ReservationState.CancellationPending && this.AllocationId == allocationId)
        {
            return Result.Success();
        }

        bool cancellationWasRequested = this.Status == ReservationState.CancellationPending &&
                                        this.AllocationId is null &&
                                        this.ReleaseRequestId.HasValue;
        if ((this.Status != ReservationState.PendingAllocation && !cancellationWasRequested) ||
            allocationId == Guid.Empty ||
            allocationVersion <= 0)
        {
            return Result.Failure(ReservationsDomainErrors.InvalidTransition);
        }

        this.AllocationId = allocationId;
        this.AllocationVersion = allocationVersion;
        this.AllocationRejection = null;
        this.Version++;
        this.UpdatedAtUtc = nowUtc;
        if (cancellationWasRequested)
        {
            this.RaiseDomainEvent(new ReservationCancellationRequestedDomainEvent(
                cancellationEventId,
                nowUtc,
                this.ScopeId,
                this.Id,
                this.PropertyId,
                allocationId,
                this.ReleaseRequestId!.Value,
                allocationVersion));
        }
        else
        {
            this.Status = ReservationState.Confirmed;
        }

        this.RaiseGuestStayChanged(cancellationEventId, nowUtc);

        return Result.Success();
    }

    public Result RejectAllocation(
        Guid allocationRequestId,
        ReservationAllocationRejection rejection,
        Guid cancellationEventId,
        DateTimeOffset nowUtc)
    {
        if (allocationRequestId != this.AllocationRequestId)
        {
            return Result.Failure(ReservationsDomainErrors.AllocationCorrelationMismatch);
        }

        if (this.Status == ReservationState.AllocationRejected && this.AllocationRejection == rejection)
        {
            return Result.Success();
        }

        bool cancellationWasRequested = this.Status == ReservationState.CancellationPending && this.AllocationId is null;
        if (this.Status != ReservationState.PendingAllocation && !cancellationWasRequested)
        {
            return Result.Failure(ReservationsDomainErrors.InvalidTransition);
        }

        this.AllocationRejection = rejection;
        this.Version++;
        this.UpdatedAtUtc = nowUtc;
        this.Status = cancellationWasRequested ? ReservationState.Cancelled : ReservationState.AllocationRejected;
        if (cancellationWasRequested)
        {
            string? cancellationActorId = this.PendingCancellationActorId;
            this.ReleaseRequestId = null;
            this.PendingCancellationActorId = null;
            this.RaiseDomainEvent(new ReservationCancelledDomainEvent(
                cancellationEventId,
                nowUtc,
                this.ScopeId,
                this.Id,
                this.PropertyId,
                this.Version,
                cancellationActorId));
        }

        this.RaiseGuestStayChanged(cancellationEventId, nowUtc);

        return Result.Success();
    }

}
