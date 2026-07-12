namespace BunkFy.Modules.Staff.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Mapping;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;

internal sealed class CreateStaffMemberCommandHandler(IStaffMemberRepository members, IScopeContext scopeContext,
    ISystemClock clock, IIdGenerator ids) : ICommandHandler<CreateStaffMemberCommand, StaffMemberDto>
{
    public async Task<Result<StaffMemberDto>> HandleAsync(CreateStaffMemberCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<StaffMemberDto>(StaffApplicationErrors.TenantRequired);
        }

        Result uniqueness = await StaffMemberUniqueness.EnsureAsync(members, command.EmployeeNumber,
            command.AuthSubjectId, null, cancellationToken).ConfigureAwait(false);
        if (uniqueness.IsFailure)
        {
            return Result.Failure<StaffMemberDto>(uniqueness.Error);
        }

        Result<StaffMember> created = StaffMember.Create(ids.NewId(), scopeContext.ScopeId,
            command.DisplayName, command.LegalName, command.WorkEmail, command.WorkPhone,
            command.EmployeeNumber, command.JobTitle, command.Department, command.AuthSubjectId,
            command.ActorId, ids.NewId(), clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<StaffMemberDto>(created.Error);
        }

        await members.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(created.Value.ToDto());
    }
}
