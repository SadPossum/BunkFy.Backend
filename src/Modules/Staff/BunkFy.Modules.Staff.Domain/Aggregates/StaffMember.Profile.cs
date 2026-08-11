namespace BunkFy.Modules.Staff.Domain.Aggregates;

using Gma.Framework.Naming;
using Gma.Framework.Results;
using BunkFy.Modules.Staff.Domain.Errors;
using BunkFy.Modules.Staff.Domain.Events;
using BunkFy.Modules.Staff.Domain.Models;
using BunkFy.Modules.Staff.Domain.ValueObjects;

public sealed partial class StaffMember
{
    public static Result<StaffMember> Create(Guid id, string tenantId, string displayName, string? legalName,
        string? workEmail, string? workPhone, string? employeeNumber, string? jobTitle, string? department,
        string? authSubjectId, string actorId, Guid eventId, DateTimeOffset nowUtc)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<StaffMember>(StaffDomainErrors.StaffMemberIdRequired);
        }

        if (!TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Result.Failure<StaffMember>(StaffDomainErrors.TenantInvalid);
        }

        Result<StaffProfile> profile = StaffProfile.Create(displayName, legalName, workEmail, workPhone,
            employeeNumber, jobTitle, department, authSubjectId);
        if (profile.IsFailure)
        {
            return Result.Failure<StaffMember>(profile.Error);
        }

        Result<StaffActorId> actor = StaffActorId.Create(actorId);
        if (actor.IsFailure)
        {
            return Result.Failure<StaffMember>(actor.Error);
        }

        if (eventId == Guid.Empty)
        {
            return Result.Failure<StaffMember>(StaffDomainErrors.EventIdRequired);
        }

        StaffMember member = new(id, scopeId);
        member.ApplyProfile(profile.Value);
        member.CreatedBy = actor.Value.Value;
        member.CreatedAtUtc = nowUtc;
        member.LastChangedBy = actor.Value.Value;
        member.LastChangedAtUtc = nowUtc;
        member.RaiseDomainEvent(new StaffMemberCreatedDomainEvent(eventId, nowUtc, member.ScopeId,
            member.Id, member.Status, member.AuthSubjectId, member.Version));
        return Result.Success(member);
    }

    public Result UpdateProfile(string displayName, string? legalName, string? workEmail, string? workPhone,
        string? employeeNumber, string? jobTitle, string? department, long expectedVersion,
        string actorId, Guid eventId, DateTimeOffset nowUtc)
    {
        Result ready = this.EnsureMutable(expectedVersion, eventId);
        if (ready.IsFailure)
        {
            return ready;
        }

        Result<StaffProfile> profile = StaffProfile.Create(displayName, legalName, workEmail, workPhone,
            employeeNumber, jobTitle, department, this.AuthSubjectId);
        if (profile.IsFailure)
        {
            return Result.Failure(profile.Error);
        }

        Result<StaffActorId> actor = StaffActorId.Create(actorId);
        if (actor.IsFailure)
        {
            return Result.Failure(actor.Error);
        }

        Result<StaffProfileUpdateOutcome> updated =
            this.ApplyProfileUpdate(
                profile.Value,
                actor.Value,
                eventId,
                nowUtc);
        return updated.IsSuccess
            ? Result.Success()
            : Result.Failure(updated.Error);
    }

    public Result<StaffProfileUpdateOutcome> UpdateProfileWithOutcome(
        StaffProfile profile,
        long expectedVersion,
        StaffActorId actor,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(actor);
        Result ready = this.EnsureMutable(expectedVersion, eventId);
        return ready.IsSuccess
            ? this.ApplyProfileUpdate(profile, actor, eventId, nowUtc)
            : Result.Failure<StaffProfileUpdateOutcome>(ready.Error);
    }

    private Result<StaffProfileUpdateOutcome> ApplyProfileUpdate(
        StaffProfile profile,
        StaffActorId actor,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        if (!string.Equals(
                this.AuthSubjectId,
                profile.AuthSubjectId,
                StringComparison.Ordinal))
        {
            return Result.Failure<StaffProfileUpdateOutcome>(
                StaffDomainErrors.AuthSubjectInvalid);
        }

        long previousVersion = this.Version;
        if (this.MatchesProfile(profile))
        {
            return Result.Success(new StaffProfileUpdateOutcome(
                previousVersion,
                previousVersion,
                Changed: false));
        }

        this.ApplyProfile(profile);
        this.Advance(actor, nowUtc);
        this.RaiseDomainEvent(new StaffMemberUpdatedDomainEvent(eventId, nowUtc, this.ScopeId,
            this.Id, this.Status, this.Version));
        return Result.Success(new StaffProfileUpdateOutcome(
            previousVersion,
            this.Version,
            Changed: true));
    }

    public Result<StaffDataRightsCorrectionOutcome> ApplyDataRightsCorrection(
        StaffProfileCorrection correction,
        long expectedVersion,
        string actorId,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(correction);
        if (expectedVersion != this.Version)
        {
            return Result.Failure<StaffDataRightsCorrectionOutcome>(
                StaffDomainErrors.VersionConflict);
        }

        if (this.Status == StaffMemberState.Anonymised)
        {
            return Result.Failure<StaffDataRightsCorrectionOutcome>(
                StaffDomainErrors.StaffAnonymised);
        }

        if (eventId == Guid.Empty)
        {
            return Result.Failure<StaffDataRightsCorrectionOutcome>(
                StaffDomainErrors.EventIdRequired);
        }

        Result<StaffActorId> actor = StaffActorId.Create(actorId);
        if (actor.IsFailure)
        {
            return Result.Failure<StaffDataRightsCorrectionOutcome>(actor.Error);
        }

        Result<StaffProfile> profile = correction.ApplyTo(this.AuthSubjectId);
        if (profile.IsFailure)
        {
            return Result.Failure<StaffDataRightsCorrectionOutcome>(profile.Error);
        }

        StaffProfileField[] changedFields = this.GetChangedFields(profile.Value);
        if (changedFields.Length == 0)
        {
            return Result.Failure<StaffDataRightsCorrectionOutcome>(
                StaffDomainErrors.CorrectionNoChanges);
        }

        long previousVersion = this.Version;
        this.ApplyProfile(profile.Value);
        this.Advance(actor.Value, nowUtc);
        this.RaiseDomainEvent(new StaffMemberUpdatedDomainEvent(
            eventId,
            nowUtc,
            this.ScopeId,
            this.Id,
            this.Status,
            this.Version));
        return Result.Success(new StaffDataRightsCorrectionOutcome(
            previousVersion,
            this.Version,
            changedFields,
            eventId,
            nowUtc));
    }

    public Result SetAuthSubject(string? authSubjectId, long expectedVersion, string actorId,
        Guid eventId, DateTimeOffset nowUtc)
    {
        Result<StaffAuthSubject> subject = StaffAuthSubject.Create(
            authSubjectId);
        if (subject.IsFailure)
        {
            return Result.Failure(subject.Error);
        }

        Result<StaffActorId> actor = StaffActorId.Create(actorId);
        return actor.IsSuccess
            ? this.SetAuthSubject(
                subject.Value,
                expectedVersion,
                actor.Value,
                eventId,
                nowUtc)
            : Result.Failure(actor.Error);
    }

    public Result SetAuthSubject(
        StaffAuthSubject authSubject,
        long expectedVersion,
        StaffActorId actor,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(authSubject);
        ArgumentNullException.ThrowIfNull(actor);
        Result ready = this.EnsureMutable(expectedVersion, eventId);
        if (ready.IsFailure)
        {
            return ready;
        }

        Result<bool> transition = this.EvaluateAuthSubjectTransition(authSubject);
        if (transition.IsFailure)
        {
            return Result.Failure(transition.Error);
        }

        if (!transition.Value)
        {
            return Result.Success();
        }

        this.AuthSubjectId = authSubject.Value;
        this.Advance(actor, nowUtc);
        this.RaiseDomainEvent(new StaffAuthSubjectChangedDomainEvent(eventId, nowUtc, this.ScopeId,
            this.Id, this.AuthSubjectId, this.Version));
        return Result.Success();
    }

    public Result<bool> EvaluateAuthSubjectChange(
        StaffAuthSubject authSubject,
        long expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(authSubject);
        Result mutable = this.EnsureMutableState(expectedVersion);
        return mutable.IsSuccess
            ? this.EvaluateAuthSubjectTransition(authSubject)
            : Result.Failure<bool>(mutable.Error);
    }

    private Result<bool> EvaluateAuthSubjectTransition(
        StaffAuthSubject authSubject)
    {
        if (string.Equals(
                this.AuthSubjectId,
                authSubject.Value,
                StringComparison.Ordinal))
        {
            return Result.Success(false);
        }

        bool currentlyLinked = this.AuthSubjectId is not null;
        bool requestedLinked = authSubject.Value is not null;
        if (currentlyLinked && requestedLinked)
        {
            return Result.Failure<bool>(
                StaffDomainErrors.AuthSubjectReplacementRequiresUnlink);
        }

        if (currentlyLinked && this.Status != StaffMemberState.Suspended)
        {
            return Result.Failure<bool>(
                StaffDomainErrors.AuthSubjectUnlinkRequiresSuspension);
        }

        if (requestedLinked && this.Status != StaffMemberState.Active)
        {
            return Result.Failure<bool>(
                StaffDomainErrors.AuthSubjectLinkRequiresActive);
        }

        return Result.Success(true);
    }

    private StaffProfileField[] GetChangedFields(StaffProfile profile)
    {
        List<StaffProfileField> changed = [];
        AddIfChanged(
            changed,
            StaffProfileField.DisplayName,
            this.DisplayName,
            profile.DisplayName);
        AddIfChanged(changed, StaffProfileField.LegalName, this.LegalName, profile.LegalName);
        AddIfChanged(changed, StaffProfileField.WorkEmail, this.WorkEmail, profile.WorkEmail);
        AddIfChanged(changed, StaffProfileField.WorkPhone, this.WorkPhone, profile.WorkPhone);
        AddIfChanged(
            changed,
            StaffProfileField.EmployeeNumber,
            this.EmployeeNumber,
            profile.EmployeeNumber);
        AddIfChanged(changed, StaffProfileField.JobTitle, this.JobTitle, profile.JobTitle);
        AddIfChanged(changed, StaffProfileField.Department, this.Department, profile.Department);
        return [.. changed];
    }

    private static void AddIfChanged(
        List<StaffProfileField> changed,
        StaffProfileField field,
        string? current,
        string? requested)
    {
        if (!string.Equals(current, requested, StringComparison.Ordinal))
        {
            changed.Add(field);
        }
    }
}
