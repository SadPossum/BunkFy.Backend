namespace BunkFy.Modules.Staff.Domain.Aggregates;

using Gma.Framework.Results;
using BunkFy.Modules.Staff.Domain.Entities;
using BunkFy.Modules.Staff.Domain.Errors;
using BunkFy.Modules.Staff.Domain.Events;
using BunkFy.Modules.Staff.Domain.ValueObjects;

public sealed partial class StaffMember
{
    public Result Suspend(long expectedVersion, string actorId, string reason, Guid eventId, DateTimeOffset nowUtc)
    {
        if (this.Status == StaffMemberState.Suspended)
        {
            return Result.Failure(StaffDomainErrors.AlreadySuspended);
        }

        Result ready = this.EnsureMutable(expectedVersion, eventId);
        return ready.IsFailure
            ? ready
            : this.ChangeLifecycle(StaffMemberState.Suspended, DateOnly.FromDateTime(nowUtc.UtcDateTime),
                actorId, reason, eventId, nowUtc);
    }

    public Result Resume(long expectedVersion, string actorId, string reason, Guid eventId, DateTimeOffset nowUtc)
    {
        if (expectedVersion != this.Version)
        {
            return Result.Failure(StaffDomainErrors.VersionConflict);
        }

        if (this.Status == StaffMemberState.Departed)
        {
            return Result.Failure(StaffDomainErrors.StaffDeparted);
        }

        if (this.Status != StaffMemberState.Suspended)
        {
            return Result.Failure(StaffDomainErrors.NotSuspended);
        }

        if (eventId == Guid.Empty)
        {
            return Result.Failure(StaffDomainErrors.EventIdRequired);
        }

        return this.ChangeLifecycle(StaffMemberState.Active, DateOnly.FromDateTime(nowUtc.UtcDateTime),
            actorId, reason, eventId, nowUtc);
    }

    public Result Depart(DateOnly effectiveOn, long expectedVersion, string actorId, string reason,
        Guid lifecycleEventId, IReadOnlyCollection<Guid> assignmentEventIds, DateTimeOffset nowUtc)
    {
        if (effectiveOn == default)
        {
            return Result.Failure(StaffDomainErrors.AssignmentDateInvalid);
        }

        if (this.Status == StaffMemberState.Departed)
        {
            return Result.Failure(StaffDomainErrors.AlreadyDeparted);
        }

        Result ready = this.EnsureMutable(expectedVersion, lifecycleEventId);
        if (ready.IsFailure)
        {
            return ready;
        }

        StaffPropertyAssignment[] current = this.assignments.Where(item => item.IsCurrent).ToArray();
        if (assignmentEventIds.Count != current.Length || assignmentEventIds.Any(id => id == Guid.Empty))
        {
            return Result.Failure(StaffDomainErrors.EventIdRequired);
        }

        if (current.Any(item => effectiveOn < item.EffectiveFrom))
        {
            return Result.Failure(StaffDomainErrors.AssignmentDateInvalid);
        }

        Result<StaffActorId> actor = StaffActorId.Create(actorId);
        Result<StaffChangeReason> changeReason = StaffChangeReason.Create(reason);
        if (actor.IsFailure || changeReason.IsFailure)
        {
            return Result.Failure(actor.IsFailure ? actor.Error : changeReason.Error);
        }

        this.Advance(actor.Value, nowUtc);
        Guid[] eventIds = assignmentEventIds.ToArray();
        for (int index = 0; index < current.Length; index++)
        {
            current[index].End(effectiveOn, actor.Value.Value, changeReason.Value.Value, nowUtc, this.Version);
            this.RaiseAssignmentEvent(current[index], eventIds[index], nowUtc);
        }

        this.Status = StaffMemberState.Departed;
        this.DepartedAtUtc = nowUtc;
        this.DepartureEffectiveOn = effectiveOn;
        this.RaiseDomainEvent(new StaffMemberLifecycleChangedDomainEvent(lifecycleEventId, nowUtc,
            this.ScopeId, this.Id, this.Status, effectiveOn, this.Version, actor.Value.Value));
        return Result.Success();
    }

    private Result ChangeLifecycle(StaffMemberState status, DateOnly effectiveOn, string actorId,
        string reason, Guid eventId, DateTimeOffset nowUtc)
    {
        Result<StaffActorId> actor = StaffActorId.Create(actorId);
        Result<StaffChangeReason> changeReason = StaffChangeReason.Create(reason);
        if (actor.IsFailure || changeReason.IsFailure)
        {
            return Result.Failure(actor.IsFailure ? actor.Error : changeReason.Error);
        }

        this.Status = status;
        this.SuspendedAtUtc = status == StaffMemberState.Suspended ? nowUtc : null;
        this.Advance(actor.Value, nowUtc);
        this.RaiseDomainEvent(new StaffMemberLifecycleChangedDomainEvent(eventId, nowUtc, this.ScopeId,
            this.Id, this.Status, effectiveOn, this.Version, actor.Value.Value));
        return Result.Success();
    }
}
