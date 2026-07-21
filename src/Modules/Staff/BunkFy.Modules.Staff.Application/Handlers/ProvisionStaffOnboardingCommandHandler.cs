namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Mapping;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class ProvisionStaffOnboardingCommandHandler(
    IStaffMemberRepository members,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<ProvisionStaffOnboardingCommand, StaffMemberDto>
{
    public async Task<Result<StaffMemberDto>> HandleAsync(
        ProvisionStaffOnboardingCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<StaffMemberDto>(StaffApplicationErrors.TenantRequired);
        }

        StaffMember? member = await members.GetByAuthSubjectAsync(
            command.AuthSubjectId,
            cancellationToken).ConfigureAwait(false);
        Result unique = await StaffMemberUniqueness.EnsureAsync(
            members,
            command.EmployeeNumber,
            command.AuthSubjectId,
            member?.Id,
            cancellationToken).ConfigureAwait(false);
        if (unique.IsFailure)
        {
            return Result.Failure<StaffMemberDto>(unique.Error);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        if (member is null)
        {
            Result<StaffMember> created = StaffMember.Create(
                ids.NewId(),
                scopeContext.ScopeId,
                command.DisplayName,
                command.LegalName,
                command.WorkEmail,
                command.WorkPhone,
                command.EmployeeNumber,
                command.JobTitle,
                command.Department,
                command.AuthSubjectId,
                command.ActorId,
                ids.NewId(),
                nowUtc);
            if (created.IsFailure)
            {
                return Result.Failure<StaffMemberDto>(created.Error);
            }

            await members.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
            return Result.Success(created.Value.ToDto());
        }

        if (member.Status == StaffMemberState.Departed)
        {
            return Result.Failure<StaffMemberDto>(StaffApplicationErrors.StaffDeparted);
        }

        Result updated = member.UpdateProfile(
            command.DisplayName,
            command.LegalName,
            command.WorkEmail,
            command.WorkPhone,
            command.EmployeeNumber,
            command.JobTitle,
            command.Department,
            member.Version,
            command.ActorId,
            ids.NewId(),
            nowUtc);
        if (updated.IsFailure)
        {
            return Result.Failure<StaffMemberDto>(updated.Error);
        }

        if (member.Status == StaffMemberState.Suspended)
        {
            Result resumed = member.Resume(
                member.Version,
                command.ActorId,
                command.Reason,
                ids.NewId(),
                nowUtc);
            if (resumed.IsFailure)
            {
                return Result.Failure<StaffMemberDto>(resumed.Error);
            }
        }

        return Result.Success(member.ToDto());
    }
}
