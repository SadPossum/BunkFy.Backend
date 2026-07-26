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
        DateTimeOffset nowUtc,
        TimeOnly? expectedArrivalTime = null,
        TimeOnly? expectedDepartureTime = null)
    {
        Result<ReservationDetailsMutation> mutation = this.ChangeGuestDetails(
            primaryGuestName,
            email,
            phone,
            guestCount,
            notes,
            expectedDetailsRevision,
            origin,
            actorId,
            adapterConnectionId,
            externalOperationId,
            correlationId,
            eventId,
            nowUtc,
            expectedArrivalTime,
            expectedDepartureTime);
        if (mutation.IsFailure)
        {
            return Result.Failure<ReservationDetailsChangeOutcome>(mutation.Error);
        }

        return Result.Success(mutation.Value.IsChanged
            ? ReservationDetailsChangeOutcome.Changed
            : ReservationDetailsChangeOutcome.Unchanged);
    }

    public Result<ReservationDataRightsCorrectionOutcome> CorrectGuestDetails(
        string primaryGuestName,
        string? email,
        string? phone,
        int guestCount,
        string? notes,
        long expectedRecordVersion,
        long expectedDetailsRevision,
        string actorId,
        Guid correlationId,
        Guid eventId,
        DateTimeOffset nowUtc,
        TimeOnly? expectedArrivalTime = null,
        TimeOnly? expectedDepartureTime = null)
    {
        if (expectedRecordVersion != this.Version)
        {
            return Result.Failure<ReservationDataRightsCorrectionOutcome>(
                ReservationsDomainErrors.VersionConflict);
        }

        Result<ReservationDetailsMutation> mutation = this.ChangeGuestDetails(
            primaryGuestName,
            email,
            phone,
            guestCount,
            notes,
            expectedDetailsRevision,
            ReservationDetailsChangeOrigin.DataRightsCorrection,
            actorId,
            adapterConnectionId: null,
            externalOperationId: null,
            correlationId,
            eventId,
            nowUtc,
            expectedArrivalTime,
            expectedDepartureTime);
        if (mutation.IsFailure)
        {
            return Result.Failure<ReservationDataRightsCorrectionOutcome>(mutation.Error);
        }

        if (!mutation.Value.IsChanged)
        {
            return Result.Failure<ReservationDataRightsCorrectionOutcome>(
                ReservationsDomainErrors.DataRightsCorrectionNoChanges);
        }

        return Result.Success(new ReservationDataRightsCorrectionOutcome(
            mutation.Value.PreviousRecordVersion,
            mutation.Value.CurrentRecordVersion,
            mutation.Value.PreviousDetailsRevision,
            mutation.Value.CurrentDetailsRevision,
            mutation.Value.ChangedFields.Select(ToDetailsField).ToArray(),
            eventId,
            correlationId,
            nowUtc));
    }

    public bool HasGuestDetails(
        string primaryGuestName,
        string? email,
        string? phone,
        int guestCount,
        string? notes,
        TimeOnly? expectedArrivalTime,
        TimeOnly? expectedDepartureTime) =>
        string.Equals(
            this.PrimaryGuestName,
            NormalizeRequired(primaryGuestName),
            StringComparison.Ordinal) &&
        string.Equals(this.Email, NormalizeOptional(email), StringComparison.Ordinal) &&
        string.Equals(this.Phone, NormalizeOptional(phone), StringComparison.Ordinal) &&
        this.GuestCount == guestCount &&
        string.Equals(this.Notes, NormalizeOptional(notes), StringComparison.Ordinal) &&
        this.ExpectedArrivalTime == expectedArrivalTime &&
        this.ExpectedDepartureTime == expectedDepartureTime;

    private Result<ReservationDetailsMutation> ChangeGuestDetails(
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
        DateTimeOffset nowUtc,
        TimeOnly? expectedArrivalTime,
        TimeOnly? expectedDepartureTime)
    {
        if (this.IsAnonymised)
        {
            return Result.Failure<ReservationDetailsMutation>(
                ReservationsDomainErrors.ReservationAlreadyAnonymised);
        }

        if (this.PendingAllocationAmendmentId.HasValue)
        {
            return Result.Failure<ReservationDetailsMutation>(
                ReservationsDomainErrors.AllocationAmendmentInProgress);
        }

        if (expectedDetailsRevision != this.DetailsRevision)
        {
            return Result.Failure<ReservationDetailsMutation>(
                ReservationsDomainErrors.DetailsRevisionConflict);
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
            return Result.Failure<ReservationDetailsMutation>(
                ReservationsDomainErrors.DetailsChangeProvenanceInvalid);
        }

        string normalizedGuestName = NormalizeRequired(primaryGuestName);
        string? normalizedEmail = NormalizeOptional(email);
        string? normalizedPhone = NormalizeOptional(phone);
        string? normalizedNotes = NormalizeOptional(notes);
        if (normalizedGuestName.Length is 0 or > PrimaryGuestNameMaxLength)
        {
            return Result.Failure<ReservationDetailsMutation>(
                ReservationsDomainErrors.PrimaryGuestNameInvalid);
        }

        if (normalizedEmail?.Length > EmailMaxLength)
        {
            return Result.Failure<ReservationDetailsMutation>(ReservationsDomainErrors.EmailInvalid);
        }

        if (normalizedPhone?.Length > PhoneMaxLength)
        {
            return Result.Failure<ReservationDetailsMutation>(ReservationsDomainErrors.PhoneInvalid);
        }

        if (normalizedNotes?.Length > NotesMaxLength)
        {
            return Result.Failure<ReservationDetailsMutation>(ReservationsDomainErrors.NotesInvalid);
        }

        if (!HasMinutePrecision(expectedArrivalTime) || !HasMinutePrecision(expectedDepartureTime))
        {
            return Result.Failure<ReservationDetailsMutation>(
                ReservationsDomainErrors.ExpectedStayTimeInvalid);
        }

        if (guestCount <= 0)
        {
            return Result.Failure<ReservationDetailsMutation>(
                ReservationsDomainErrors.GuestCountInvalid);
        }

        List<string> changedFields = [];
        AddChanged(changedFields, nameof(this.PrimaryGuestName), this.PrimaryGuestName, normalizedGuestName);
        AddChanged(changedFields, nameof(this.Email), this.Email, normalizedEmail);
        AddChanged(changedFields, nameof(this.Phone), this.Phone, normalizedPhone);
        AddChanged(changedFields, nameof(this.GuestCount), this.GuestCount, guestCount);
        AddChanged(changedFields, nameof(this.Notes), this.Notes, normalizedNotes);
        AddChanged(changedFields, nameof(this.ExpectedArrivalTime), this.ExpectedArrivalTime, expectedArrivalTime);
        AddChanged(changedFields, nameof(this.ExpectedDepartureTime), this.ExpectedDepartureTime, expectedDepartureTime);
        if (changedFields.Count == 0)
        {
            return Result.Success(new ReservationDetailsMutation(
                IsChanged: false,
                this.Version,
                this.Version,
                this.DetailsRevision,
                this.DetailsRevision,
                []));
        }

        ReservationDetailsSnapshot before = this.CaptureDetails();
        long previousRecordVersion = this.Version;
        long fromRevision = this.DetailsRevision;
        this.PrimaryGuestName = normalizedGuestName;
        this.PrimaryGuestNameSearch = NormalizeSearch(normalizedGuestName)!;
        this.Email = normalizedEmail;
        this.EmailSearch = NormalizeSearch(normalizedEmail);
        this.Phone = normalizedPhone;
        this.PhoneSearch = NormalizeSearch(normalizedPhone);
        this.GuestCount = guestCount;
        this.Notes = normalizedNotes;
        this.ExpectedArrivalTime = expectedArrivalTime;
        this.ExpectedDepartureTime = expectedDepartureTime;
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
        return Result.Success(new ReservationDetailsMutation(
            IsChanged: true,
            previousRecordVersion,
            this.Version,
            fromRevision,
            this.DetailsRevision,
            changedFields));
    }

    private static ReservationDetailsField ToDetailsField(string field) => field switch
    {
        nameof(PrimaryGuestName) => ReservationDetailsField.PrimaryGuestName,
        nameof(Email) => ReservationDetailsField.Email,
        nameof(Phone) => ReservationDetailsField.Phone,
        nameof(GuestCount) => ReservationDetailsField.GuestCount,
        nameof(Notes) => ReservationDetailsField.Notes,
        nameof(ExpectedArrivalTime) => ReservationDetailsField.ExpectedArrivalTime,
        nameof(ExpectedDepartureTime) => ReservationDetailsField.ExpectedDepartureTime,
        _ => throw new InvalidOperationException(
            $"Reservation details field '{field}' is not correctable.")
    };

    private sealed record ReservationDetailsMutation(
        bool IsChanged,
        long PreviousRecordVersion,
        long CurrentRecordVersion,
        long PreviousDetailsRevision,
        long CurrentDetailsRevision,
        IReadOnlyCollection<string> ChangedFields);
}
