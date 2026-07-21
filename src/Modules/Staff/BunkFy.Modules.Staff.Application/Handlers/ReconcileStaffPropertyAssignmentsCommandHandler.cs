namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.Entities;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class ReconcileStaffPropertyAssignmentsCommandHandler(
    IStaffMemberRepository members,
    IStaffPropertyProjectionRepository properties,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<ReconcileStaffPropertyAssignmentsCommand, IReadOnlyCollection<Guid>>
{
    public async Task<Result<IReadOnlyCollection<Guid>>> HandleAsync(
        ReconcileStaffPropertyAssignmentsCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<IReadOnlyCollection<Guid>>(StaffApplicationErrors.TenantRequired);
        }

        Guid[] desiredPropertyIds = command.PropertyIds
            .Distinct()
            .Order()
            .ToArray();
        if (!await properties.AreAllActiveAsync(desiredPropertyIds, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result.Failure<IReadOnlyCollection<Guid>>(
                StaffApplicationErrors.PropertyUnavailable);
        }

        StaffMember? member = await members.GetAsync(command.StaffMemberId, cancellationToken)
            .ConfigureAwait(false);
        if (member is null)
        {
            return Result.Failure<IReadOnlyCollection<Guid>>(
                StaffApplicationErrors.StaffMemberNotFound);
        }

        if (member.Status == StaffMemberState.Departed)
        {
            return Result.Failure<IReadOnlyCollection<Guid>>(StaffApplicationErrors.StaffDeparted);
        }

        if (member.Status == StaffMemberState.Suspended)
        {
            return Result.Failure<IReadOnlyCollection<Guid>>(StaffApplicationErrors.StaffSuspended);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        DateOnly effectiveOn = DateOnly.FromDateTime(nowUtc.UtcDateTime);
        HashSet<Guid> desired = desiredPropertyIds.ToHashSet();
        StaffPropertyAssignment[] current = member.Assignments
            .Where(assignment => assignment.IsCurrent)
            .OrderBy(assignment => assignment.PropertyId)
            .ToArray();

        foreach (StaffPropertyAssignment stale in current.Where(
                     assignment => !desired.Contains(assignment.PropertyId)))
        {
            DateOnly effectiveTo = stale.EffectiveFrom > effectiveOn
                ? stale.EffectiveFrom
                : effectiveOn;
            Result ended = member.UnassignProperty(
                stale.PropertyId,
                effectiveTo,
                member.Version,
                command.ActorId,
                command.Reason,
                ids.NewId(),
                nowUtc);
            if (ended.IsFailure)
            {
                return Result.Failure<IReadOnlyCollection<Guid>>(ended.Error);
            }
        }

        HashSet<Guid> retained = member.Assignments
            .Where(assignment => assignment.IsCurrent)
            .Select(assignment => assignment.PropertyId)
            .ToHashSet();
        foreach (Guid propertyId in desiredPropertyIds.Where(id => !retained.Contains(id)))
        {
            Result assigned = member.AssignProperty(
                ids.NewId(),
                propertyId,
                propertyJobTitle: null,
                isPrimary: false,
                effectiveOn,
                member.Version,
                command.ActorId,
                ids.NewId(),
                nowUtc);
            if (assigned.IsFailure)
            {
                return Result.Failure<IReadOnlyCollection<Guid>>(assigned.Error);
            }
        }

        return Result.Success<IReadOnlyCollection<Guid>>(desiredPropertyIds);
    }
}
