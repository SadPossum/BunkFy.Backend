namespace BunkFy.Modules.Staff.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Mapping;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;

internal sealed class DepartStaffMemberCommandHandler(IStaffMemberRepository members,
    StaffLifecyclePolicyEvaluator policies, ISystemClock clock, IIdGenerator ids)
    : ICommandHandler<DepartStaffMemberCommand, StaffDirectoryMemberDto>
{
    public async Task<Result<StaffDirectoryMemberDto>> HandleAsync(DepartStaffMemberCommand command,
        CancellationToken cancellationToken)
    {
        StaffMember? member = await members.GetAsync(command.StaffMemberId, cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return Result.Failure<StaffDirectoryMemberDto>(StaffApplicationErrors.StaffMemberNotFound);
        }

        Guid[] assignmentEventIds = member.Assignments.Where(item => item.IsCurrent)
            .Select(_ => ids.NewId()).ToArray();
        Guid transitionId = ids.NewId();
        StaffStatus previousStatus = StaffMappings.MapStatus(member.Status);
        Result departed = member.Depart(command.EffectiveOn, command.ExpectedVersion, command.ActorId,
            command.Reason, transitionId, assignmentEventIds, clock.UtcNow);
        if (departed.IsFailure)
        {
            return Result.Failure<StaffDirectoryMemberDto>(departed.Error);
        }

        Result prepared = await policies.PrepareAsync(new StaffLifecyclePolicyContext(
            transitionId, member.ScopeId, member.Id, member.AuthSubjectId,
            StaffLifecycleTransition.Depart, previousStatus, StaffStatus.Departed,
            command.EffectiveOn, command.ExpectedVersion, member.Version, command.ActorId),
            cancellationToken).ConfigureAwait(false);
        return prepared.IsSuccess
            ? Result.Success(member.ToDirectoryDto())
            : Result.Failure<StaffDirectoryMemberDto>(prepared.Error);
    }
}
