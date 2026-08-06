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

internal sealed class SetStaffAuthSubjectCommandHandler(
    IStaffMemberRepository members,
    StaffMemberMutationCoordinator mutations,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<SetStaffAuthSubjectCommand, StaffDirectoryMemberDto>
{
    public async Task<Result<StaffDirectoryMemberDto>> HandleAsync(SetStaffAuthSubjectCommand command,
        CancellationToken cancellationToken)
    {
        StaffMember? member = await mutations.AcquireOperationalAsync(
                command.StaffMemberId,
                cancellationToken)
            .ConfigureAwait(false);
        if (member is null)
        {
            return Result.Failure<StaffDirectoryMemberDto>(StaffApplicationErrors.StaffMemberNotFound);
        }

        Result uniqueness = await StaffMemberUniqueness.EnsureAsync(members, member.EmployeeNumber,
            command.AuthSubjectId, member.Id, cancellationToken).ConfigureAwait(false);
        if (uniqueness.IsFailure)
        {
            return Result.Failure<StaffDirectoryMemberDto>(uniqueness.Error);
        }

        Result changed = member.SetAuthSubject(command.AuthSubjectId, command.ExpectedVersion,
            command.ActorId, ids.NewId(), clock.UtcNow);
        return changed.IsSuccess
            ? Result.Success(member.ToDirectoryDto())
            : Result.Failure<StaffDirectoryMemberDto>(changed.Error);
    }
}
