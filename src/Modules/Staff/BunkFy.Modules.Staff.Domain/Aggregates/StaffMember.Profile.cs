namespace BunkFy.Modules.Staff.Domain.Aggregates;

using Gma.Framework.Naming;
using Gma.Framework.Results;
using BunkFy.Modules.Staff.Domain.Errors;
using BunkFy.Modules.Staff.Domain.Events;
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

        this.ApplyProfile(profile.Value);
        this.Advance(actor.Value, nowUtc);
        this.RaiseDomainEvent(new StaffMemberUpdatedDomainEvent(eventId, nowUtc, this.ScopeId,
            this.Id, this.Status, this.Version));
        return Result.Success();
    }

    public Result SetAuthSubject(string? authSubjectId, long expectedVersion, string actorId,
        Guid eventId, DateTimeOffset nowUtc)
    {
        if (!StaffProfile.TryNormalizeAuthSubject(authSubjectId, out string? normalized))
        {
            return Result.Failure(StaffDomainErrors.AuthSubjectInvalid);
        }

        if (string.Equals(this.AuthSubjectId, normalized, StringComparison.Ordinal))
        {
            return Result.Success();
        }

        Result ready = this.EnsureMutable(expectedVersion, eventId);
        if (ready.IsFailure)
        {
            return ready;
        }

        Result<StaffActorId> actor = StaffActorId.Create(actorId);
        if (actor.IsFailure)
        {
            return Result.Failure(actor.Error);
        }

        this.AuthSubjectId = normalized;
        this.Advance(actor.Value, nowUtc);
        this.RaiseDomainEvent(new StaffAuthSubjectChangedDomainEvent(eventId, nowUtc, this.ScopeId,
            this.Id, this.AuthSubjectId, this.Version));
        return Result.Success();
    }
}
