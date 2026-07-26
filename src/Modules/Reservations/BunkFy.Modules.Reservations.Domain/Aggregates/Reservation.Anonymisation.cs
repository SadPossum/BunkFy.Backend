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

        ReservationRedactionState redaction =
            this.RedactOwnedDetails(actor, nowUtc, nowUtc);

        return Result.Success(new ReservationAnonymisationOutcome(
            redaction.PreviousVersion,
            this.Version,
            redaction.PreviousDetailsRevision,
            this.DetailsRevision,
            redaction.RemovedGuestLinkCount,
            eventId,
            actor,
            redaction.ChangedFields,
            nowUtc));
    }

    public Result<ReservationAnonymisationRestoreOutcome>
        RestoreAnonymisation(
            long expectedResultingVersion,
            string actorId,
            Guid eventId,
            DateTimeOffset originallyCompletedAtUtc,
            DateTimeOffset replayedAtUtc)
    {
        string actor = actorId?.Trim() ?? string.Empty;
        DateTimeOffset completedAtUtc =
            originallyCompletedAtUtc.ToUniversalTime();
        DateTimeOffset restoredAtUtc = replayedAtUtc.ToUniversalTime();
        if (this.IsAnonymised ||
            expectedResultingVersion <= 1 ||
            this.Version != expectedResultingVersion - 1 ||
            actor.Length is 0 or > ActorIdMaxLength ||
            eventId == Guid.Empty ||
            completedAtUtc == default ||
            restoredAtUtc == default ||
            restoredAtUtc < completedAtUtc)
        {
            return Result.Failure<ReservationAnonymisationRestoreOutcome>(
                ReservationsDomainErrors.ReservationAnonymisationRestoreStateInvalid);
        }

        ReservationRedactionState redaction =
            this.RedactOwnedDetails(
                actor,
                completedAtUtc,
                restoredAtUtc);
        if (this.Version != expectedResultingVersion)
        {
            return Result.Failure<ReservationAnonymisationRestoreOutcome>(
                ReservationsDomainErrors.ReservationAnonymisationRestoreStateInvalid);
        }

        return Result.Success(new ReservationAnonymisationRestoreOutcome(
            redaction.PreviousVersion,
            this.Version,
            redaction.PreviousDetailsRevision,
            this.DetailsRevision,
            redaction.RemovedGuestLinkCount,
            eventId,
            actor,
            redaction.ChangedFields,
            completedAtUtc,
            restoredAtUtc));
    }

    public bool MatchesAnonymisedState(
        long expectedVersion,
        long expectedDetailsRevision,
        DateTimeOffset completedAtUtc) =>
        this.IsAnonymised &&
        this.Version == expectedVersion &&
        this.DetailsRevision == expectedDetailsRevision &&
        this.HasAnonymisedValues(completedAtUtc);

    public bool MatchesAnonymisedState(
        long expectedVersion,
        DateTimeOffset completedAtUtc) =>
        this.IsAnonymised &&
        this.Version == expectedVersion &&
        this.HasAnonymisedValues(completedAtUtc);

    private bool HasAnonymisedValues(
        DateTimeOffset completedAtUtc) =>
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
        this.PendingAllocationAmendmentId is null &&
        this.PendingAllocationAmendmentRequestFingerprint is null &&
        this.PendingArrival is null &&
        this.PendingDeparture is null &&
        this.PendingExpectedArrivalTime is null &&
        this.PendingExpectedDepartureTime is null &&
        this.PendingInventoryUnitIds is null &&
        this.PendingGuestCount is null &&
        this.PendingNotes is null &&
        this.PendingDetailsChangeOrigin ==
            ReservationDetailsChangeOrigin.Unknown &&
        this.PendingDetailsActorId is null &&
        this.PendingDetailsAdapterConnectionId is null &&
        this.PendingDetailsExternalOperationId is null &&
        this.PendingDetailsCorrelationId is null &&
        this.guests.Count == 0;

    private ReservationRedactionState RedactOwnedDetails(
        string actor,
        DateTimeOffset anonymisedAtUtc,
        DateTimeOffset changedAtUtc)
    {
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
        AddChanged(
            changedFields,
            nameof(this.SourceReference),
            this.SourceReference,
            after: null);
        AddChanged(changedFields, nameof(this.Notes), this.Notes, after: null);
        if (removedGuestLinkCount > 0)
        {
            changedFields.Add(nameof(this.Guests));
        }

        if (this.PendingAllocationAmendmentId.HasValue)
        {
            changedFields.Add(nameof(this.PendingAllocationAmendmentId));
        }

        this.PrimaryGuestName = AnonymisedGuestName;
        this.PrimaryGuestNameSearch = NormalizeSearch(AnonymisedGuestName)!;
        this.Email = null;
        this.EmailSearch = null;
        this.Phone = null;
        this.PhoneSearch = null;
        this.SourceReference = null;
        this.Notes = null;
        this.ClearPendingAllocationAmendment();
        this.guests.Clear();
        this.DetailsRevision++;
        this.LastDetailsChangeOrigin =
            ReservationDetailsChangeOrigin.DataRightsAnonymisation;
        this.LastDetailsActorId = actor;
        this.LastDetailsAdapterConnectionId = null;
        this.LastDetailsExternalOperationId = null;
        this.LastDetailsChangedAtUtc = changedAtUtc;
        this.IsAnonymised = true;
        this.AnonymisedAtUtc = anonymisedAtUtc;
        this.Version++;
        this.UpdatedAtUtc = changedAtUtc;
        return new(
            previousVersion,
            previousDetailsRevision,
            removedGuestLinkCount,
            changedFields);
    }

    private sealed record ReservationRedactionState(
        long PreviousVersion,
        long PreviousDetailsRevision,
        int RemovedGuestLinkCount,
        IReadOnlyCollection<string> ChangedFields);
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

public sealed record ReservationAnonymisationRestoreOutcome(
    long PreviousVersion,
    long CurrentVersion,
    long PreviousDetailsRevision,
    long CurrentDetailsRevision,
    int RemovedGuestLinkCount,
    Guid EventId,
    string ActorId,
    IReadOnlyCollection<string> ChangedFields,
    DateTimeOffset OriginallyCompletedAtUtc,
    DateTimeOffset ReplayedAtUtc);
