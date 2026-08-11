namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.Errors;
using BunkFy.Modules.Staff.Domain.ValueObjects;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class BootstrapStaffIdentityCommandHandler(
    IStaffMemberRepository members,
    IStaffCreationOperationLock creationLock,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<BootstrapStaffIdentityCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        BootstrapStaffIdentityCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled ||
            string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<Unit>(StaffApplicationErrors.TenantRequired);
        }

        if (command.OperationId == Guid.Empty)
        {
            return Result.Failure<Unit>(
                StaffApplicationErrors.CreationOperationInvalid);
        }

        Result<StaffAuthSubject> authSubject = StaffAuthSubject.Create(
            command.AuthSubjectId);
        if (authSubject.IsFailure || authSubject.Value.Value is null)
        {
            return Result.Failure<Unit>(StaffDomainErrors.AuthSubjectInvalid);
        }

        string normalizedAuthSubject = authSubject.Value.Value;

        await creationLock.AcquireAsync(
                scopeContext.ScopeId,
                command.OperationId,
                cancellationToken)
            .ConfigureAwait(false);
        StaffMember? operationOwner = await members.GetForSafetyTransitionAsync(
            command.OperationId,
            cancellationToken).ConfigureAwait(false);
        if (operationOwner is not null)
        {
            return string.Equals(
                operationOwner.AuthSubjectId,
                normalizedAuthSubject,
                StringComparison.Ordinal)
                ? Result.Success(Unit.Value)
                : Result.Failure<Unit>(
                    StaffApplicationErrors.CreationOperationConflict);
        }

        if (await members.AuthSubjectExistsAsync(
                normalizedAuthSubject,
                exceptStaffMemberId: null,
                cancellationToken).ConfigureAwait(false))
        {
            return Result.Success(Unit.Value);
        }

        Result<StaffProfile> profile = StaffProfile.Create(
            command.DisplayName,
            legalName: null,
            command.WorkEmail,
            workPhone: null,
            employeeNumber: null,
            jobTitle: null,
            department: null,
            normalizedAuthSubject);
        if (profile.IsFailure)
        {
            return Result.Failure<Unit>(profile.Error);
        }

        Result<StaffActorId> actor = StaffActorId.Create(command.ActorId);
        if (actor.IsFailure)
        {
            return Result.Failure<Unit>(actor.Error);
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
            profile.Value.AuthSubjectId,
            actor.Value.Value,
            ids.NewId(),
            clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<Unit>(created.Error);
        }

        await members.AddAsync(created.Value, cancellationToken)
            .ConfigureAwait(false);
        return Result.Success(Unit.Value);
    }
}
