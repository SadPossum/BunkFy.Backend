namespace BunkFy.Modules.Guests.Domain.Aggregates;

using BunkFy.Modules.Guests.Domain.Errors;
using BunkFy.Modules.Guests.Domain.Events;
using BunkFy.Modules.Guests.Domain.Models;
using Gma.Framework.Results;

public sealed partial class GuestProfile
{
    public const string AnonymisedDisplayName = "Anonymised guest";

    public Result<GuestProfileAnonymisationOutcome> Anonymise(
        long expectedVersion,
        string actorId,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        if (expectedVersion != this.Version)
        {
            return Result.Failure<GuestProfileAnonymisationOutcome>(
                GuestsDomainErrors.VersionConflict);
        }

        if (this.Status == GuestProfileState.Anonymised)
        {
            return Result.Failure<GuestProfileAnonymisationOutcome>(
                GuestsDomainErrors.GuestAlreadyAnonymised);
        }

        if (this.Status != GuestProfileState.Active)
        {
            return Result.Failure<GuestProfileAnonymisationOutcome>(
                GuestsDomainErrors.GuestNotActiveForAnonymisation);
        }

        Result<string> actor = NormalizeActor(actorId);
        if (actor.IsFailure)
        {
            return Result.Failure<GuestProfileAnonymisationOutcome>(actor.Error);
        }

        if (eventId == Guid.Empty)
        {
            return Result.Failure<GuestProfileAnonymisationOutcome>(
                GuestsDomainErrors.EventIdRequired);
        }

        if (nowUtc == default)
        {
            return Result.Failure<GuestProfileAnonymisationOutcome>(
                GuestsDomainErrors.AnonymisationTimestampInvalid);
        }

        long previousVersion = this.Version;
        this.DisplayName = AnonymisedDisplayName;
        this.DisplayNameSearch = NormalizeRequiredSearch(AnonymisedDisplayName);
        this.LegalName = null;
        this.LegalNameSearch = null;
        this.Email = null;
        this.EmailSearch = null;
        this.Phone = null;
        this.PhoneSearch = null;
        this.DateOfBirth = null;
        this.NationalityCountryCode = null;
        this.PreferredLanguageTag = null;
        this.Notes = null;
        this.Status = GuestProfileState.Anonymised;
        this.AnonymisedAtUtc = nowUtc;
        this.LastChangedBy = actor.Value;
        this.LastChangedAtUtc = nowUtc;
        this.Version++;
        this.RaiseDomainEvent(new GuestProfileAnonymisedDomainEvent(
            eventId,
            nowUtc,
            this.ScopeId,
            this.Id,
            this.Version));
        return Result.Success(new GuestProfileAnonymisationOutcome(
            previousVersion,
            this.Version,
            eventId,
            nowUtc));
    }

    public bool MatchesAnonymisedState(long version, DateTimeOffset completedAtUtc) =>
        this.Status == GuestProfileState.Anonymised &&
        this.Version == version &&
        this.AnonymisedAtUtc == completedAtUtc &&
        string.Equals(this.DisplayName, AnonymisedDisplayName, StringComparison.Ordinal) &&
        string.Equals(
            this.DisplayNameSearch,
            NormalizeRequiredSearch(AnonymisedDisplayName),
            StringComparison.Ordinal) &&
        this.LegalName is null &&
        this.LegalNameSearch is null &&
        this.Email is null &&
        this.EmailSearch is null &&
        this.Phone is null &&
        this.PhoneSearch is null &&
        this.DateOfBirth is null &&
        this.NationalityCountryCode is null &&
        this.PreferredLanguageTag is null &&
        this.Notes is null &&
        this.ArchivedAtUtc is null;
}
