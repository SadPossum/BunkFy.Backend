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

internal sealed class AssignStaffPropertyCommandHandler(IStaffMemberRepository members,
    IStaffPropertyProjectionRepository properties, ISystemClock clock, IIdGenerator ids)
    : ICommandHandler<AssignStaffPropertyCommand, StaffMemberDto>
{
    public async Task<Result<StaffMemberDto>> HandleAsync(AssignStaffPropertyCommand command,
        CancellationToken cancellationToken)
    {
        if (!await properties.IsActiveAsync(command.PropertyId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<StaffMemberDto>(StaffApplicationErrors.PropertyUnavailable);
        }

        StaffMember? member = await members.GetAsync(command.StaffMemberId, cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return Result.Failure<StaffMemberDto>(StaffApplicationErrors.StaffMemberNotFound);
        }

        Result assigned = member.AssignProperty(ids.NewId(), command.PropertyId, command.PropertyJobTitle,
            command.IsPrimary, command.EffectiveFrom, command.ExpectedVersion, command.ActorId,
            ids.NewId(), clock.UtcNow);
        return assigned.IsSuccess
            ? Result.Success(member.ToDto())
            : Result.Failure<StaffMemberDto>(assigned.Error);
    }
}
