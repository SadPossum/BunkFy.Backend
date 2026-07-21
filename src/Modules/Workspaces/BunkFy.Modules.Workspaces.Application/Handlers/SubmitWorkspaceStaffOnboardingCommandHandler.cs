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
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.Options;

internal sealed class SubmitWorkspaceStaffOnboardingCommandHandler(
    IWorkspaceStaffOnboardingRepository applications,
    IWorkspaceStaffAccessPlanRepository plans,
    IOrganizationJoinTokenInspector joinTokens,
    IAuthMemberContactReader contacts,
    IOptions<WorkspaceStaffOnboardingOptions> options,
    IScopeContextAccessor scopeContext,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<SubmitWorkspaceStaffOnboardingCommand, WorkspaceStaffOnboardingDto>
{
    public async Task<Result<WorkspaceStaffOnboardingDto>> HandleAsync(
        SubmitWorkspaceStaffOnboardingCommand command,
        CancellationToken cancellationToken)
    {
        (Guid OrganizationId, Guid SourceId)? authority = await this.InspectTokenAsync(
            command.SourceKind,
            command.Token,
            cancellationToken).ConfigureAwait(false);
        if (!authority.HasValue)
        {
            return Result.Failure<WorkspaceStaffOnboardingDto>(
                WorkspaceStaffOnboardingApplicationErrors.JoinTokenInvalid);
        }

        scopeContext.SetScope(authority.Value.OrganizationId.ToString("D"));

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

    private async Task<(Guid OrganizationId, Guid SourceId)?> InspectTokenAsync(
        WorkspaceStaffOnboardingSourceKind sourceKind,
        string token,
        CancellationToken cancellationToken)
    {
        if (sourceKind == WorkspaceStaffOnboardingSourceKind.Invitation)
        {
            OrganizationJoinTokenInspection<OrganizationInvitationPreviewDto> inspected =
                await joinTokens.InspectInvitationAsync(token, cancellationToken).ConfigureAwait(false);
            return inspected.Preview is { Status: OrganizationInvitationStatus.Pending } preview
                ? (preview.OrganizationId, preview.InvitationId)
                : null;
        }

        if (sourceKind == WorkspaceStaffOnboardingSourceKind.EnrollmentLink)
        {
            OrganizationJoinTokenInspection<OrganizationEnrollmentPreviewDto> inspected =
                await joinTokens.InspectEnrollmentAsync(token, cancellationToken).ConfigureAwait(false);
            return inspected.Preview is { Status: OrganizationEnrollmentLinkStatus.Active } preview
                ? (preview.OrganizationId, preview.EnrollmentLinkId)
                : null;
        }

        return null;
    }
}
