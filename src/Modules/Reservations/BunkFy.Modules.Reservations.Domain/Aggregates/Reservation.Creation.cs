namespace BunkFy.Modules.Reservations.Domain.Aggregates;

using Gma.Framework.Results;
using BunkFy.Modules.Reservations.Domain.Entities;
using BunkFy.Modules.Reservations.Domain.Errors;
using BunkFy.Modules.Reservations.Domain.Events;

public sealed partial class Reservation
{
    public static Result<Reservation> Create(
        Guid reservationId,
        string scopeId,
        Guid propertyId,
        Guid allocationRequestId,
        DateOnly arrival,
        DateOnly departure,
        IReadOnlyCollection<Guid> inventoryUnitIds,
        string primaryGuestName,
        string? email,
        string? phone,
        int guestCount,
        ReservationSource source,
        string? sourceSystem,
        string? sourceReference,
        string? notes,
        Guid eventId,
        Guid detailsEventId,
        ReservationDetailsChangeOrigin initialDetailsOrigin,
        string? initialDetailsActorId,
        Guid? initialAdapterConnectionId,
        Guid? initialExternalOperationId,
        Guid initialCorrelationId,
        DateTimeOffset nowUtc)
    {
        if (reservationId == Guid.Empty)
        {
            return Result.Failure<Reservation>(ReservationsDomainErrors.ReservationIdRequired);
        }

        if (eventId == Guid.Empty || detailsEventId == Guid.Empty || eventId == detailsEventId)
        {
            return Result.Failure<Reservation>(ReservationsDomainErrors.DetailsChangeProvenanceInvalid);
        }

        string? normalizedInitialActorId = NormalizeOptional(initialDetailsActorId);
        bool adapterOrigin = initialDetailsOrigin == ReservationDetailsChangeOrigin.Adapter;
        if (initialDetailsOrigin == ReservationDetailsChangeOrigin.Unknown || !Enum.IsDefined(initialDetailsOrigin) ||
            normalizedInitialActorId?.Length > ActorIdMaxLength || initialCorrelationId == Guid.Empty ||
            adapterOrigin != initialAdapterConnectionId.HasValue ||
            adapterOrigin != initialExternalOperationId.HasValue ||
            initialAdapterConnectionId == Guid.Empty || initialExternalOperationId == Guid.Empty)
        {
            return Result.Failure<Reservation>(ReservationsDomainErrors.DetailsChangeProvenanceInvalid);
        }

        if (propertyId == Guid.Empty)
        {
            return Result.Failure<Reservation>(ReservationsDomainErrors.PropertyIdRequired);
        }

        if (allocationRequestId == Guid.Empty)
        {
            return Result.Failure<Reservation>(ReservationsDomainErrors.AllocationRequestIdRequired);
        }

        if (arrival >= departure)
        {
            return Result.Failure<Reservation>(ReservationsDomainErrors.StayRangeInvalid);
        }

        ArgumentNullException.ThrowIfNull(inventoryUnitIds);
        Guid[] units = inventoryUnitIds.ToArray();
        if (units.Length is 0 or > MaximumRequestedUnits || units.Any(id => id == Guid.Empty) || units.Distinct().Count() != units.Length)
        {
            return Result.Failure<Reservation>(ReservationsDomainErrors.RequestedUnitsInvalid);
        }

        string normalizedGuestName = NormalizeRequired(primaryGuestName);
        if (normalizedGuestName.Length is 0 or > PrimaryGuestNameMaxLength)
        {
            return Result.Failure<Reservation>(ReservationsDomainErrors.PrimaryGuestNameInvalid);
        }

        string? normalizedEmail = NormalizeOptional(email);
        string? normalizedPhone = NormalizeOptional(phone);
        string? normalizedNotes = NormalizeOptional(notes);
        if (normalizedEmail?.Length > EmailMaxLength)
        {
            return Result.Failure<Reservation>(ReservationsDomainErrors.EmailInvalid);
        }

        if (normalizedPhone?.Length > PhoneMaxLength)
        {
            return Result.Failure<Reservation>(ReservationsDomainErrors.PhoneInvalid);
        }

        if (normalizedNotes?.Length > NotesMaxLength)
        {
            return Result.Failure<Reservation>(ReservationsDomainErrors.NotesInvalid);
        }

        if (guestCount <= 0)
        {
            return Result.Failure<Reservation>(ReservationsDomainErrors.GuestCountInvalid);
        }

        string? normalizedSourceSystem = NormalizeOptional(sourceSystem)?.ToLowerInvariant();
        string? normalizedSourceReference = NormalizeOptional(sourceReference);
        bool sourceValid = source switch
        {
            ReservationSource.Direct => normalizedSourceSystem is null && normalizedSourceReference is null,
            ReservationSource.External => normalizedSourceSystem is { Length: > 0 and <= SourceSystemMaxLength } &&
                                          normalizedSourceReference is { Length: > 0 and <= SourceReferenceMaxLength },
            _ => false
        };
        if (!sourceValid)
        {
            return Result.Failure<Reservation>(ReservationsDomainErrors.SourceInvalid);
        }

        Reservation reservation = new(
            reservationId,
            scopeId,
            propertyId,
            allocationRequestId,
            arrival,
            departure,
            units,
            normalizedGuestName,
            normalizedEmail,
            normalizedPhone,
            guestCount,
            source,
            normalizedSourceSystem,
            normalizedSourceReference,
            normalizedNotes,
            nowUtc)
        {
            LastDetailsChangeOrigin = initialDetailsOrigin,
            LastDetailsActorId = normalizedInitialActorId,
            LastDetailsAdapterConnectionId = initialAdapterConnectionId,
            LastDetailsExternalOperationId = initialExternalOperationId
        };
        reservation.RaiseDomainEvent(new ReservationCreatedDomainEvent(
            eventId,
            nowUtc,
            reservation.ScopeId,
            reservation.Id,
            reservation.PropertyId,
            reservation.AllocationRequestId,
            reservation.Arrival,
            reservation.Departure,
            units,
            reservation.Version));
        reservation.RaiseDomainEvent(new ReservationDetailsChangedDomainEvent(
            detailsEventId,
            nowUtc,
            reservation.ScopeId,
            reservation.Id,
            reservation.PropertyId,
            fromRevision: 0,
            toRevision: reservation.DetailsRevision,
            initialDetailsOrigin,
            normalizedInitialActorId,
            initialAdapterConnectionId,
            initialExternalOperationId,
            initialCorrelationId,
            [
                nameof(Arrival),
                nameof(Departure),
                nameof(RequestedUnits),
                nameof(PrimaryGuestName),
                nameof(Email),
                nameof(Phone),
                nameof(GuestCount),
                nameof(Notes)
            ],
            before: null,
            reservation.CaptureDetails()));
        return Result.Success(reservation);
    }

}
