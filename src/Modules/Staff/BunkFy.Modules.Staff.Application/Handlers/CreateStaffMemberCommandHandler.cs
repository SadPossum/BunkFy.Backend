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
using BunkFy.Modules.Staff.Domain.ValueObjects;

internal sealed class CreateStaffMemberCommandHandler(
    IStaffMemberRepository members,
    IStaffCreationOperationLock creationLock,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<CreateStaffMemberCommand, StaffDirectoryMemberDto>
{
    public async Task<Result<StaffDirectoryMemberDto>> HandleAsync(CreateStaffMemberCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<StaffDirectoryMemberDto>(StaffApplicationErrors.TenantRequired);
        }

        if (command.OperationId == Guid.Empty)
        {
            return Result.Failure<StaffDirectoryMemberDto>(
                StaffApplicationErrors.CreationOperationInvalid);
        }

        Result<StaffProfile> profile = StaffProfile.Create(
            command.DisplayName,
            command.LegalName,
            command.WorkEmail,
            command.WorkPhone,
            command.EmployeeNumber,
            command.JobTitle,
            command.Department,
            authSubjectId: null);
        if (profile.IsFailure)
        {
            return Result.Failure<StaffDirectoryMemberDto>(profile.Error);
        }

        Result<StaffActorId> actor = StaffActorId.Create(command.ActorId);
        if (actor.IsFailure)
        {
            return Result.Failure<StaffDirectoryMemberDto>(actor.Error);
        }

        await creationLock.AcquireAsync(
                scopeContext.ScopeId,
                command.OperationId,
                cancellationToken)
            .ConfigureAwait(false);
        StaffMember? existing = await members.GetAsync(
            command.OperationId,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.MatchesCreation(profile.Value)
                ? Result.Success(existing.ToDirectoryDto())
                : Result.Failure<StaffDirectoryMemberDto>(
                    StaffApplicationErrors.CreationOperationConflict);
        }

        if (await members.GetForSafetyTransitionAsync(
                command.OperationId,
                cancellationToken).ConfigureAwait(false) is not null)
        {
            return Result.Failure<StaffDirectoryMemberDto>(
                StaffApplicationErrors.StaffMemberNotFound);
        }

        Result uniqueness = await StaffMemberUniqueness.EnsureEmployeeNumberAsync(
            members,
            command.EmployeeNumber,
            exceptStaffMemberId: null,
            cancellationToken).ConfigureAwait(false);
        if (uniqueness.IsFailure)
        {
            return Result.Failure<StaffDirectoryMemberDto>(uniqueness.Error);
        }

        Result<StaffMember> created = StaffMember.Create(
            command.OperationId,
            scopeContext.ScopeId,
            profile.Value.DisplayName,
            profile.Value.LegalName,
            profile.Value.WorkEmail,
            profile.Value.WorkPhone,
            profile.Value.EmployeeNumber,
            profile.Value.JobTitle,
            profile.Value.Department,
            authSubjectId: null,
            actor.Value.Value,
            ids.NewId(),
            clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<StaffDirectoryMemberDto>(created.Error);
        }

        await members.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(created.Value.ToDirectoryDto());
    }
}
