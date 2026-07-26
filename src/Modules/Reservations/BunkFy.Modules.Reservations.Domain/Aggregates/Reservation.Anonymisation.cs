namespace BunkFy.Modules.Reservations.Domain.Aggregates;

using BunkFy.Modules.Reservations.Domain.Errors;
using Gma.Framework.Results;

public sealed partial class Reservation
{
    public Result<ReservationAnonymisationOutcome> Anonymise(
        long expectedVersion,
        long expectedDetailsRevision,
        string actorId,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        if (expectedVersion != this.Version)
        {
            return Result.Failure<ReservationAnonymisationOutcome>(
                ReservationsDomainErrors.VersionConflict);
        }

        if (expectedDetailsRevision != this.DetailsRevision)
        {
            return Result.Failure<ReservationAnonymisationOutcome>(
                ReservationsDomainErrors.DetailsRevisionConflict);
        }

        if (this.IsAnonymised)
        {
            return Result.Failure<ReservationAnonymisationOutcome>(
                ReservationsDomainErrors.ReservationAlreadyAnonymised);
        }

        if (this.Status is not (
                ReservationState.AllocationRejected or
                ReservationState.Cancelled or
                ReservationState.NoShow or
                ReservationState.CheckedOut) ||
            this.PendingAllocationAmendmentId.HasValue)
        {
            return Result.Failure<ReservationAnonymisationOutcome>(
                ReservationsDomainErrors.ReservationNotEligibleForAnonymisation);
        }

        string actor = actorId?.Trim() ?? string.Empty;
        if (actor.Length is 0 or > ActorIdMaxLength ||
            eventId == Guid.Empty ||
            nowUtc == default)
        {
            return Result.Failure<ReservationAnonymisationOutcome>(
                ReservationsDomainErrors.ReservationAnonymisationProvenanceInvalid);
        }

        long previousVersion = this.Version;
        long previousDetailsRevision = this.DetailsRevision;
        int removedGuestLinkCount = this.guests.Count;
        List<string> changedFields = [nameof(this.IsAnonymised)];
        AddChanged(
            changedFields,
            nameof(this.PrimaryGuestName),
            this.PrimaryGuestName,
            AnonymisedGuestName);
        AddChanged(changedFields, nameof(this.Email), this.Email, after: null);
        AddChanged(changedFields, nameof(this.Phone), this.Phone, after: null);
        AddChanged(changedFields, nameof(this.SourceReference), this.SourceReference, after: null);
        AddChanged(changedFields, nameof(this.Notes), this.Notes, after: null);
        if (removedGuestLinkCount > 0)
        {
            changedFields.Add(nameof(this.Guests));
        }

        this.PrimaryGuestName = AnonymisedGuestName;
        this.PrimaryGuestNameSearch = NormalizeSearch(AnonymisedGuestName)!;
        this.Email = null;
        this.EmailSearch = null;
        this.Phone = null;
        this.PhoneSearch = null;
        this.SourceReference = null;
        this.Notes = null;
        this.PendingPrimaryGuestName = null;
        this.PendingPrimaryGuestNameSearch = null;
        this.PendingEmail = null;
        this.PendingEmailSearch = null;
        this.PendingPhone = null;
        this.PendingPhoneSearch = null;
        this.PendingNotes = null;
        this.guests.Clear();
        this.DetailsRevision++;
        this.LastDetailsChangeOrigin =
            ReservationDetailsChangeOrigin.DataRightsAnonymisation;
        this.LastDetailsActorId = actor;
        this.LastDetailsAdapterConnectionId = null;
        this.LastDetailsExternalOperationId = null;
        this.LastDetailsChangedAtUtc = nowUtc;
        this.IsAnonymised = true;
        this.AnonymisedAtUtc = nowUtc;
        this.Version++;
        this.UpdatedAtUtc = nowUtc;

        return Result.Success(new ReservationAnonymisationOutcome(
            previousVersion,
            this.Version,
            previousDetailsRevision,
            this.DetailsRevision,
            removedGuestLinkCount,
            eventId,
            actor,
            changedFields,
            nowUtc));
    }

    public bool MatchesAnonymisedState(
        long expectedVersion,
        long expectedDetailsRevision,
        DateTimeOffset completedAtUtc) =>
        this.IsAnonymised &&
        this.Version == expectedVersion &&
        this.DetailsRevision == expectedDetailsRevision &&
        this.AnonymisedAtUtc == completedAtUtc &&
        string.Equals(
            this.PrimaryGuestName,
            AnonymisedGuestName,
            StringComparison.Ordinal) &&
        string.Equals(
            this.PrimaryGuestNameSearch,
            NormalizeSearch(AnonymisedGuestName),
            StringComparison.Ordinal) &&
        this.Email is null &&
        this.EmailSearch is null &&
        this.Phone is null &&
        this.PhoneSearch is null &&
        this.SourceReference is null &&
        this.Notes is null &&
        this.PendingPrimaryGuestName is null &&
        this.PendingPrimaryGuestNameSearch is null &&
        this.PendingEmail is null &&
        this.PendingEmailSearch is null &&
        this.PendingPhone is null &&
        this.PendingPhoneSearch is null &&
        this.PendingNotes is null &&
        this.guests.Count == 0;
}

public sealed record ReservationAnonymisationOutcome(
    long PreviousVersion,
    long CurrentVersion,
    long PreviousDetailsRevision,
    long CurrentDetailsRevision,
    int RemovedGuestLinkCount,
    Guid EventId,
    string ActorId,
    IReadOnlyCollection<string> ChangedFields,
    DateTimeOffset CompletedAtUtc);
