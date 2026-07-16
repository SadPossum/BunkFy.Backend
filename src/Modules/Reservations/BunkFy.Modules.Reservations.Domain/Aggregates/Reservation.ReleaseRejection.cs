namespace BunkFy.Modules.Reservations.Domain.Aggregates;

using BunkFy.Modules.Reservations.Domain.Errors;
using Gma.Framework.Results;

public sealed partial class Reservation
{
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
        this.PendingCancellationActorId = null;
        this.LastReleaseRejectionCode = rejectionCode;
        this.Version++;
        this.UpdatedAtUtc = nowUtc;
        this.RaiseGuestStayChanged(eventId, nowUtc);
        return Result.Success();
    }
}
