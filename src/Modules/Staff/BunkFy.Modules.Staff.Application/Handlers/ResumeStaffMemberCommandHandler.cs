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

internal sealed class ResumeStaffMemberCommandHandler(IStaffMemberRepository members,
    StaffLifecyclePolicyEvaluator policies, ISystemClock clock, IIdGenerator ids)
    : ICommandHandler<ResumeStaffMemberCommand, StaffDirectoryMemberDto>
{
    public async Task<Result<StaffDirectoryMemberDto>> HandleAsync(
        ResumeStaffMemberCommand command,
        CancellationToken cancellationToken)
    {
        StaffMember? member = await members.GetAsync(command.StaffMemberId, cancellationToken)
            .ConfigureAwait(false);
        if (member is null)
        {
            return Result.Failure<StaffDirectoryMemberDto>(StaffApplicationErrors.StaffMemberNotFound);
        }

        Guid transitionId = ids.NewId();
        DateTimeOffset nowUtc = clock.UtcNow;
        StaffStatus previousStatus = StaffMappings.MapStatus(member.Status);
        Result changed = member.Resume(
            command.ExpectedVersion, command.ActorId, command.Reason, transitionId, nowUtc);
        if (changed.IsFailure)
        {
            return Result.Failure<StaffDirectoryMemberDto>(changed.Error);
        }

        Result prepared = await policies.PrepareAsync(new StaffLifecyclePolicyContext(
            transitionId, member.ScopeId, member.Id, member.AuthSubjectId,
            StaffLifecycleTransition.Resume, previousStatus, StaffStatus.Active,
            DateOnly.FromDateTime(nowUtc.UtcDateTime), command.ExpectedVersion, member.Version,
            command.ActorId), cancellationToken).ConfigureAwait(false);
        return prepared.IsSuccess
            ? Result.Success(member.ToDirectoryDto())
            : Result.Failure<StaffDirectoryMemberDto>(prepared.Error);
    }
}
