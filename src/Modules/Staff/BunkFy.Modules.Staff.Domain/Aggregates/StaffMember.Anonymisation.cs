namespace BunkFy.Modules.Staff.Domain.Aggregates;

using BunkFy.Modules.Staff.Domain.Errors;
using BunkFy.Modules.Staff.Domain.Events;
using BunkFy.Modules.Staff.Domain.Models;
using BunkFy.Modules.Staff.Domain.Entities;
using BunkFy.Modules.Staff.Domain.ValueObjects;
using Gma.Framework.Results;

public sealed partial class StaffMember
{
    public const string AnonymisedDisplayName =
        "Anonymised staff member";

    public Result<StaffMemberAnonymisationOutcome> Anonymise(
        long expectedVersion,
        string actorId,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        if (expectedVersion != this.Version)
        {
            return Result.Failure<StaffMemberAnonymisationOutcome>(
                StaffDomainErrors.VersionConflict);
        }

        if (this.Status == StaffMemberState.Anonymised)
        {
            return Result.Failure<StaffMemberAnonymisationOutcome>(
                StaffDomainErrors.StaffAnonymised);
        }

        if (this.Status != StaffMemberState.Departed ||
            this.DepartedAtUtc is null ||
            this.DepartureEffectiveOn is null ||
            this.assignments.Any(assignment => assignment.IsCurrent))
        {
            return Result.Failure<StaffMemberAnonymisationOutcome>(
                StaffDomainErrors.AnonymisationTransitionInvalid);
        }

        Result<StaffActorId> actor = StaffActorId.Create(actorId);
        if (actor.IsFailure)
        {
            return Result.Failure<StaffMemberAnonymisationOutcome>(
                actor.Error);
        }

        if (eventId == Guid.Empty)
        {
            return Result.Failure<StaffMemberAnonymisationOutcome>(
                StaffDomainErrors.EventIdRequired);
        }

        DateTimeOffset timestamp = nowUtc.ToUniversalTime();
        if (timestamp == default ||
            timestamp < this.DepartedAtUtc.Value.ToUniversalTime())
        {
            return Result.Failure<StaffMemberAnonymisationOutcome>(
                StaffDomainErrors.AnonymisationTimestampInvalid);
        }

        return this.ApplyAnonymisedState(
            actor.Value,
            eventId,
            timestamp,
            timestamp);
    }

    public Result<StaffMemberAnonymisationOutcome> RestoreAnonymisation(
        string actorId,
        Guid eventId,
        DateTimeOffset originallyCompletedAtUtc,
        DateTimeOffset replayedAtUtc)
    {
        if (this.Status != StaffMemberState.Departed ||
            this.DepartedAtUtc is null ||
            this.DepartureEffectiveOn is null ||
            this.assignments.Any(assignment => assignment.IsCurrent))
        {
            return Result.Failure<StaffMemberAnonymisationOutcome>(
                StaffDomainErrors.AnonymisationRestoreTransitionInvalid);
        }

        Result<StaffActorId> actor = StaffActorId.Create(actorId);
        if (actor.IsFailure)
        {
            return Result.Failure<StaffMemberAnonymisationOutcome>(
                actor.Error);
        }

        if (eventId == Guid.Empty)
        {
            return Result.Failure<StaffMemberAnonymisationOutcome>(
                StaffDomainErrors.EventIdRequired);
        }

        DateTimeOffset completedAtUtc =
            originallyCompletedAtUtc.ToUniversalTime();
        DateTimeOffset occurredAtUtc = replayedAtUtc.ToUniversalTime();
        if (completedAtUtc == default ||
            occurredAtUtc == default ||
            completedAtUtc <
                this.DepartedAtUtc.Value.ToUniversalTime() ||
            occurredAtUtc < completedAtUtc)
        {
            return Result.Failure<StaffMemberAnonymisationOutcome>(
                StaffDomainErrors.AnonymisationTimestampInvalid);
        }

        return this.ApplyAnonymisedState(
            actor.Value,
            eventId,
            completedAtUtc,
            occurredAtUtc);
    }

    private Result<StaffMemberAnonymisationOutcome> ApplyAnonymisedState(
        StaffActorId actor,
        Guid eventId,
        DateTimeOffset completedAtUtc,
        DateTimeOffset occurredAtUtc)
    {
        long previousVersion = this.Version;
        this.DisplayName = AnonymisedDisplayName;
        this.DisplayNameSearch =
            AnonymisedDisplayName.ToUpperInvariant();
        this.LegalName = null;
        this.LegalNameSearch = null;
        this.WorkEmail = null;
        this.WorkEmailSearch = null;
        this.WorkPhone = null;
        this.WorkPhoneSearch = null;
        this.EmployeeNumber = null;
        this.EmployeeNumberSearch = null;
        this.JobTitle = null;
        this.Department = null;
        this.AuthSubjectId = null;
        foreach (StaffPropertyAssignment assignment in this.assignments)
        {
            assignment.AnonymiseFreeText();
        }

        this.Status = StaffMemberState.Anonymised;
        this.SuspendedAtUtc = null;
        this.AnonymisedAtUtc = completedAtUtc;
        this.Advance(actor, occurredAtUtc);
        this.RaiseDomainEvent(new StaffMemberAnonymisedDomainEvent(
            eventId,
            occurredAtUtc,
            this.ScopeId,
            this.Id,
            this.Version));
        return Result.Success(new StaffMemberAnonymisationOutcome(
            previousVersion,
            this.Version,
            eventId,
            occurredAtUtc));
    }

    public bool MatchesAnonymisedState(
        long version,
        DateTimeOffset completedAtUtc) =>
        this.Version == version &&
        this.Status == StaffMemberState.Anonymised &&
        this.AnonymisedAtUtc ==
            completedAtUtc.ToUniversalTime() &&
        this.HasScrubbedPersonalData();

    private bool HasScrubbedPersonalData() =>
        string.Equals(
            this.DisplayName,
            AnonymisedDisplayName,
            StringComparison.Ordinal) &&
        string.Equals(
            this.DisplayNameSearch,
            AnonymisedDisplayName.ToUpperInvariant(),
            StringComparison.Ordinal) &&
        this.LegalName is null &&
        this.LegalNameSearch is null &&
        this.WorkEmail is null &&
        this.WorkEmailSearch is null &&
        this.WorkPhone is null &&
        this.WorkPhoneSearch is null &&
        this.EmployeeNumber is null &&
        this.EmployeeNumberSearch is null &&
        this.JobTitle is null &&
        this.Department is null &&
        this.AuthSubjectId is null &&
        this.assignments.All(assignment =>
            assignment.PropertyJobTitle is null &&
            assignment.UnassignmentReason is null);
}
