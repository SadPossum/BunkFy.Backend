namespace BunkFy.Modules.Reservations.Domain.Aggregates;

using Gma.Framework.Results;
using BunkFy.Modules.Reservations.Domain.Entities;
using BunkFy.Modules.Reservations.Domain.Errors;
using BunkFy.Modules.Reservations.Domain.Events;

public sealed partial class Reservation
{
    public Result<ReservationDetailsChangeOutcome> UpdateGuestDetails(
        string primaryGuestName,
        string? email,
        string? phone,
        int guestCount,
        string? notes,
        long expectedDetailsRevision,
        ReservationDetailsChangeOrigin origin,
        string actorId,
        Guid? adapterConnectionId,
        Guid? externalOperationId,
        Guid correlationId,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        if (this.PendingAllocationAmendmentId.HasValue)
        {
            return Result.Failure<ReservationDetailsChangeOutcome>(ReservationsDomainErrors.AllocationAmendmentInProgress);
        }

        if (expectedDetailsRevision != this.DetailsRevision)
        {
            return Result.Failure<ReservationDetailsChangeOutcome>(ReservationsDomainErrors.DetailsRevisionConflict);
        }

        string normalizedActorId = actorId?.Trim() ?? string.Empty;
        bool adapterOrigin = origin == ReservationDetailsChangeOrigin.Adapter;
        if (origin == ReservationDetailsChangeOrigin.Unknown || !Enum.IsDefined(origin) ||
            normalizedActorId.Length is 0 or > ActorIdMaxLength ||
            correlationId == Guid.Empty || eventId == Guid.Empty ||
            adapterOrigin != adapterConnectionId.HasValue ||
            adapterOrigin != externalOperationId.HasValue ||
            adapterConnectionId == Guid.Empty || externalOperationId == Guid.Empty)
        {
            return Result.Failure<ReservationDetailsChangeOutcome>(ReservationsDomainErrors.DetailsChangeProvenanceInvalid);
        }

        string normalizedGuestName = NormalizeRequired(primaryGuestName);
        string? normalizedEmail = NormalizeOptional(email);
        string? normalizedPhone = NormalizeOptional(phone);
        string? normalizedNotes = NormalizeOptional(notes);
        if (normalizedGuestName.Length is 0 or > PrimaryGuestNameMaxLength)
        {
            return Result.Failure<ReservationDetailsChangeOutcome>(ReservationsDomainErrors.PrimaryGuestNameInvalid);
        }

        if (normalizedEmail?.Length > EmailMaxLength)
        {
            return Result.Failure<ReservationDetailsChangeOutcome>(ReservationsDomainErrors.EmailInvalid);
        }

        if (normalizedPhone?.Length > PhoneMaxLength)
        {
            return Result.Failure<ReservationDetailsChangeOutcome>(ReservationsDomainErrors.PhoneInvalid);
        }

        if (normalizedNotes?.Length > NotesMaxLength)
        {
            return Result.Failure<ReservationDetailsChangeOutcome>(ReservationsDomainErrors.NotesInvalid);
        }

        if (guestCount <= 0)
        {
            return Result.Failure<ReservationDetailsChangeOutcome>(ReservationsDomainErrors.GuestCountInvalid);
        }

        List<string> changedFields = [];
        AddChanged(changedFields, nameof(this.PrimaryGuestName), this.PrimaryGuestName, normalizedGuestName);
        AddChanged(changedFields, nameof(this.Email), this.Email, normalizedEmail);
        AddChanged(changedFields, nameof(this.Phone), this.Phone, normalizedPhone);
        AddChanged(changedFields, nameof(this.GuestCount), this.GuestCount, guestCount);
        AddChanged(changedFields, nameof(this.Notes), this.Notes, normalizedNotes);
        if (changedFields.Count == 0)
        {
            return Result.Success(ReservationDetailsChangeOutcome.Unchanged);
        }

        ReservationDetailsSnapshot before = this.CaptureDetails();
        long fromRevision = this.DetailsRevision;
        this.PrimaryGuestName = normalizedGuestName;
        this.Email = normalizedEmail;
        this.Phone = normalizedPhone;
        this.GuestCount = guestCount;
        this.Notes = normalizedNotes;
        this.DetailsRevision++;
        this.LastDetailsChangeOrigin = origin;
        this.LastDetailsActorId = normalizedActorId;
        this.LastDetailsAdapterConnectionId = adapterConnectionId;
        this.LastDetailsExternalOperationId = externalOperationId;
        this.LastDetailsChangedAtUtc = nowUtc;
        this.Version++;
        this.UpdatedAtUtc = nowUtc;
        this.RaiseDomainEvent(new ReservationDetailsChangedDomainEvent(
            eventId,
            nowUtc,
            this.ScopeId,
            this.Id,
            this.PropertyId,
            fromRevision,
            this.DetailsRevision,
            origin,
            normalizedActorId,
            adapterConnectionId,
            externalOperationId,
            correlationId,
            changedFields,
            before,
            this.CaptureDetails()));
        this.RaiseGuestStayChanged(eventId, nowUtc);
        return Result.Success(ReservationDetailsChangeOutcome.Changed);
    }

}
