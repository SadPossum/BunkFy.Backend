namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Mapping;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Gma.Modules.Auth.Contracts;
using Microsoft.Extensions.Options;

internal sealed class SubmitWorkspaceStaffOnboardingCommandHandler(
    IWorkspaceStaffOnboardingRepository applications,
    IWorkspaceStaffAccessPlanRepository plans,
    WorkspaceStaffJoinTokenAuthorityResolver authorityResolver,
    IAuthMemberContactReader contacts,
    IOptions<WorkspaceStaffOnboardingOptions> options,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<SubmitWorkspaceStaffOnboardingCommand, WorkspaceStaffOnboardingDto>
{
    public async Task<Result<WorkspaceStaffOnboardingDto>> HandleAsync(
        SubmitWorkspaceStaffOnboardingCommand command,
        CancellationToken cancellationToken)
    {
        WorkspaceStaffJoinTokenAuthority? authority = await authorityResolver.ResolveAsync(
            command.SourceKind,
            command.Token,
            cancellationToken).ConfigureAwait(false);
        if (!authority.HasValue)
        {
            return Result.Failure<WorkspaceStaffOnboardingDto>(
                WorkspaceStaffOnboardingApplicationErrors.JoinTokenInvalid);
        }

        if (!string.Equals(
            scopeContext.ScopeId,
            authority.Value.OrganizationId.ToString("D"),
            StringComparison.Ordinal))
        {
            return Result.Failure<WorkspaceStaffOnboardingDto>(
                WorkspaceStaffOnboardingApplicationErrors.AccessPlanUnavailable);
        }

        WorkspaceStaffOnboardingSource sourceKind = command.SourceKind.ToDomain();
        WorkspaceStaffAccessPlan? plan = await plans.GetAsync(
            authority.Value.SourceId,
            cancellationToken).ConfigureAwait(false);
        if (plan is null || plan.SourceKind != sourceKind ||
            plan.Status != WorkspaceStaffAccessPlanState.Active)
        {
            return Result.Failure<WorkspaceStaffOnboardingDto>(
                WorkspaceStaffOnboardingApplicationErrors.AccessPlanUnavailable);
        }

        if (!Guid.TryParse(command.SubjectId, out Guid memberId))
        {
            return Result.Failure<WorkspaceStaffOnboardingDto>(
                WorkspaceStaffOnboardingApplicationErrors.VerifiedIdentityRequired);
        }

        string? verifiedEmail = await contacts.GetPreferredVerifiedEmailAsync(
            options.Value.GlobalAuthScopeId,
            memberId,
            cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(verifiedEmail))
        {
            return Result.Failure<WorkspaceStaffOnboardingDto>(
                WorkspaceStaffOnboardingApplicationErrors.VerifiedIdentityRequired);
        }

        WorkspaceStaffOnboarding? application = await applications.GetBySourceAndSubjectAsync(
            sourceKind,
            authority.Value.SourceId,
            command.SubjectId,
            cancellationToken).ConfigureAwait(false);
        DateTimeOffset nowUtc = clock.UtcNow;
        if (application is null)
        {
            Result<WorkspaceStaffOnboarding> created = WorkspaceStaffOnboarding.Create(
                ids.NewId(),
                authority.Value.OrganizationId.ToString("D"),
                sourceKind,
                authority.Value.SourceId,
                command.SubjectId,
                verifiedEmail,
                command.DisplayName,
                command.LegalName,
                command.WorkEmail,
                command.WorkPhone,
                command.EmployeeNumber,
                command.JobTitle,
                command.Department,
                nowUtc);
            if (created.IsFailure)
            {
                return Result.Failure<WorkspaceStaffOnboardingDto>(created.Error);
            }

            application = created.Value;
            await applications.AddAsync(application, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            Result updated = application.UpdateSubmission(
                verifiedEmail,
                command.DisplayName,
                command.LegalName,
                command.WorkEmail,
                command.WorkPhone,
                command.EmployeeNumber,
                command.JobTitle,
                command.Department,
                nowUtc);
            if (updated.IsFailure)
            {
                return Result.Failure<WorkspaceStaffOnboardingDto>(updated.Error);
            }
        }

        return Result.Success(application.ToDto());
    }
}
