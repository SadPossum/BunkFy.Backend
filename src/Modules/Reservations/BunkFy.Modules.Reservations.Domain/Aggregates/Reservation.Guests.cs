namespace BunkFy.Modules.Reservations.Domain.Aggregates;

using Gma.Framework.Results;
using BunkFy.Modules.Reservations.Domain.Entities;
using BunkFy.Modules.Reservations.Domain.Errors;
using BunkFy.Modules.Reservations.Domain.Events;

public sealed partial class Reservation
{
    public Result<bool> LinkGuest(
        Guid guestId,
        ReservationGuestRole role,
        bool replaceExistingRole,
        long expectedVersion,
        string actorId,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        if (guestId == Guid.Empty || role is not ReservationGuestRole.Primary || eventId == Guid.Empty)
        {
            return Result.Failure<bool>(ReservationsDomainErrors.ReservationGuestLinkInvalid);
        }

        string normalizedActorId = actorId?.Trim() ?? string.Empty;
        if (normalizedActorId.Length is 0 or > ActorIdMaxLength)
        {
            return Result.Failure<bool>(ReservationsDomainErrors.ReservationGuestLinkInvalid);
        }

        ReservationGuest? current = this.guests.SingleOrDefault(guest => guest.IsCurrent && guest.Role == role);
        if (current?.GuestId == guestId)
        {
            return Result.Success(false);
        }

        if (expectedVersion != this.Version)
        {
            return Result.Failure<bool>(ReservationsDomainErrors.VersionConflict);
        }

        if (current is not null && !replaceExistingRole)
        {
            return Result.Failure<bool>(ReservationsDomainErrors.ReservationGuestRoleOccupied);
        }

        long newVersion = this.Version + 1;
        current?.Deactivate(
            normalizedActorId,
            nowUtc,
            newVersion,
            this.Arrival,
            this.Departure,
            this.Status,
            this.CheckedInBusinessDate,
            this.NoShowBusinessDate,
            this.CheckedOutBusinessDate);

        ReservationGuest? linkedGuest = this.guests.SingleOrDefault(guest => guest.GuestId == guestId);
        if (linkedGuest is null)
        {
            linkedGuest = new ReservationGuest(
                guestId,
                this.ScopeId,
                this.Id,
                role,
                normalizedActorId,
                nowUtc,
                newVersion);
            this.guests.Add(linkedGuest);
        }
        else
        {
            linkedGuest.Activate(role, normalizedActorId, nowUtc, newVersion);
        }

        this.Version = newVersion;
        this.UpdatedAtUtc = nowUtc;
        if (current is not null)
        {
            this.RaiseGuestStayChanged(eventId, nowUtc, current);
        }

        this.RaiseDomainEvent(new ReservationGuestLinkedDomainEvent(
            eventId,
            nowUtc,
            this.ScopeId,
            this.Id,
            this.PropertyId,
            guestId,
            role,
            this.Arrival,
            this.Departure,
            this.Status,
            this.CheckedInBusinessDate,
            this.NoShowBusinessDate,
            this.CheckedOutBusinessDate,
            this.Version));
        return Result.Success(true);
    }

}
