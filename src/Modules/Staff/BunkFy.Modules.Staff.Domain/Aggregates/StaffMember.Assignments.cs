namespace BunkFy.Modules.Staff.Domain.Aggregates;

using Gma.Framework.Results;
using BunkFy.Modules.Staff.Domain.Entities;
using BunkFy.Modules.Staff.Domain.Errors;
using BunkFy.Modules.Staff.Domain.ValueObjects;

public sealed partial class StaffMember
{
    public Result<StaffPropertyAssignment> AssignProperty(Guid assignmentId, Guid propertyId,
        string? propertyJobTitle, bool isPrimary, DateOnly effectiveFrom, long expectedVersion,
        string actorId, Guid eventId, DateTimeOffset nowUtc)
    {
        if (effectiveFrom == default)
        {
            return Result.Failure<StaffPropertyAssignment>(StaffDomainErrors.AssignmentDateInvalid);
        }

        if (assignmentId == Guid.Empty)
        {
            return Result.Failure<StaffPropertyAssignment>(StaffDomainErrors.AssignmentIdRequired);
        }

        if (propertyId == Guid.Empty)
        {
            return Result.Failure<StaffPropertyAssignment>(StaffDomainErrors.PropertyIdRequired);
        }

        string? normalizedTitle = StaffProfile.NormalizeOptional(propertyJobTitle);
        if (normalizedTitle?.Length > StaffPropertyAssignment.JobTitleMaxLength)
        {
            return Result.Failure<StaffPropertyAssignment>(StaffDomainErrors.JobTitleInvalid);
        }

        Result<StaffActorId> actor = StaffActorId.Create(actorId);
        if (actor.IsFailure)
        {
            return Result.Failure<StaffPropertyAssignment>(actor.Error);
        }

        Result ready = this.EnsureActive(expectedVersion, eventId);
        if (ready.IsFailure)
        {
            return Result.Failure<StaffPropertyAssignment>(ready.Error);
        }

        StaffPropertyAssignment? current = this.assignments.FirstOrDefault(item =>
            item.PropertyId == propertyId && item.IsCurrent);
        if (current is not null && current.PropertyJobTitle == normalizedTitle &&
            current.IsPrimary == isPrimary && current.EffectiveFrom == effectiveFrom)
        {
            return Result.Success(current);
        }

        if (current is not null)
        {
            return Result.Failure<StaffPropertyAssignment>(StaffDomainErrors.AssignmentAlreadyExists);
        }

        if (isPrimary && this.assignments.Any(item => item.IsCurrent && item.IsPrimary))
        {
            return Result.Failure<StaffPropertyAssignment>(StaffDomainErrors.PrimaryAssignmentExists);
        }

        this.Advance(actor.Value, nowUtc);
        StaffPropertyAssignment assignment = new(assignmentId, this.ScopeId, this.Id, propertyId, normalizedTitle,
            isPrimary, effectiveFrom, actor.Value.Value, nowUtc, this.Version);
        this.assignments.Add(assignment);
        this.RaiseAssignmentEvent(assignment, eventId, nowUtc);
        return Result.Success(assignment);
    }

    public Result<StaffPropertyAssignment> UnassignProperty(Guid propertyId, DateOnly effectiveTo,
        long expectedVersion, string actorId, string reason, Guid eventId, DateTimeOffset nowUtc)
    {
        if (propertyId == Guid.Empty)
        {
            return Result.Failure<StaffPropertyAssignment>(StaffDomainErrors.PropertyIdRequired);
        }

        if (effectiveTo == default)
        {
            return Result.Failure<StaffPropertyAssignment>(StaffDomainErrors.AssignmentDateInvalid);
        }

        Result<StaffActorId> actor = StaffActorId.Create(actorId);
        Result<StaffChangeReason> changeReason = StaffChangeReason.Create(reason);
        if (actor.IsFailure || changeReason.IsFailure)
        {
            return Result.Failure<StaffPropertyAssignment>(actor.IsFailure ? actor.Error : changeReason.Error);
        }

        Result ready = this.EnsureMutable(expectedVersion, eventId);
        if (ready.IsFailure)
        {
            return Result.Failure<StaffPropertyAssignment>(ready.Error);
        }

        StaffPropertyAssignment? current = this.assignments.FirstOrDefault(item =>
            item.PropertyId == propertyId && item.IsCurrent);
        if (current is null)
        {
            return Result.Failure<StaffPropertyAssignment>(
                StaffDomainErrors.AssignmentNotFound);
        }

        if (effectiveTo < current.EffectiveFrom)
        {
            return Result.Failure<StaffPropertyAssignment>(StaffDomainErrors.AssignmentDateInvalid);
        }

        this.Advance(actor.Value, nowUtc);
        current.End(effectiveTo, actor.Value.Value, changeReason.Value.Value, nowUtc, this.Version);
        this.RaiseAssignmentEvent(current, eventId, nowUtc);
        return Result.Success(current);
    }
}
