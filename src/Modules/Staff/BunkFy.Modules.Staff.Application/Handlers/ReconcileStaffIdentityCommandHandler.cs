namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Domain.Aggregates;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class ReconcileStaffIdentityCommandHandler(
    IStaffMemberRepository members,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<ReconcileStaffIdentityCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        ReconcileStaffIdentityCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<Unit>(StaffApplicationErrors.TenantRequired);
        }

        StaffMember? member = await members.GetByAuthSubjectAsync(
            command.AuthSubjectId,
            cancellationToken).ConfigureAwait(false);

        if (member is null)
        {
            if (!command.IsActive)
            {
                return Result.Success(Unit.Value);
            }

            Result<StaffMember> created = StaffMember.Create(
                ids.NewId(),
                scopeContext.ScopeId,
                command.DisplayName,
                null,
                command.WorkEmail,
                null,
                null,
                null,
                null,
                command.AuthSubjectId,
                command.ActorId,
                ids.NewId(),
                clock.UtcNow);
            if (created.IsFailure)
            {
                return Result.Failure<Unit>(created.Error);
            }

            await members.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
            return Result.Success(Unit.Value);
        }

        Result changed = command.IsActive
            ? ResumeWhenNeeded(member, command, ids.NewId(), clock.UtcNow)
            : SuspendWhenNeeded(member, command, ids.NewId(), clock.UtcNow);
        return changed.IsSuccess
            ? Result.Success(Unit.Value)
            : Result.Failure<Unit>(changed.Error);
    }

    private static Result ResumeWhenNeeded(
        StaffMember member,
        ReconcileStaffIdentityCommand command,
        Guid eventId,
        DateTimeOffset nowUtc) => member.Status switch
        {
            StaffMemberState.Active => Result.Success(),
            StaffMemberState.Suspended => member.Resume(
                member.Version, command.ActorId, command.Reason, eventId, nowUtc),
            _ => Result.Failure(StaffApplicationErrors.StaffDeparted)
        };

    private static Result SuspendWhenNeeded(
        StaffMember member,
        ReconcileStaffIdentityCommand command,
        Guid eventId,
        DateTimeOffset nowUtc) => member.Status switch
        {
            StaffMemberState.Suspended or StaffMemberState.Departed => Result.Success(),
            _ => member.Suspend(member.Version, command.ActorId, command.Reason, eventId, nowUtc)
        };
}
