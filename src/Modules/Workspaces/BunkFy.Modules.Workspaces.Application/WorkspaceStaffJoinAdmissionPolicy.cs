namespace BunkFy.Modules.Workspaces.Application;

using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

internal sealed class WorkspaceStaffJoinAdmissionPolicy(
    IOptions<WorkspaceStaffOnboardingOptions> options,
    IWorkspaceAuthoritativeScope authoritativeScope)
    : IOrganizationJoinAdmissionPolicy
{
    public async ValueTask<bool> IsAllowedAsync(
        OrganizationJoinAdmissionContext context,
        CancellationToken cancellationToken = default)
    {
        WorkspaceStaffOnboardingSource sourceKind = context.Operation switch
        {
            OrganizationJoinAdmissionOperation.AcceptInvitation =>
                WorkspaceStaffOnboardingSource.Invitation,
            OrganizationJoinAdmissionOperation.ClaimEnrollment or
                OrganizationJoinAdmissionOperation.ApproveEnrollment =>
                WorkspaceStaffOnboardingSource.EnrollmentLink,
            _ => WorkspaceStaffOnboardingSource.Unknown
        };
        if (sourceKind == WorkspaceStaffOnboardingSource.Unknown ||
            !Guid.TryParse(context.ApplicantSubjectId, out Guid memberId))
        {
            return false;
        }

        return await authoritativeScope.RunAsync(
            context.OrganizationId,
            async services =>
            {
                IWorkspaceStaffAccessPlanRepository plans = services
                    .GetRequiredService<IWorkspaceStaffAccessPlanRepository>();
                WorkspaceStaffAccessPlan? plan = await plans.GetAsync(
                    context.SourceId,
                    cancellationToken).ConfigureAwait(false);
                if (plan is null || plan.SourceKind != sourceKind ||
                    plan.Status != WorkspaceStaffAccessPlanState.Active)
                {
                    return false;
                }

                IWorkspaceStaffOnboardingRepository applications = services
                    .GetRequiredService<IWorkspaceStaffOnboardingRepository>();
                WorkspaceStaffOnboarding? application = await applications
                    .GetBySourceAndSubjectAsync(
                        sourceKind,
                        context.SourceId,
                        context.ApplicantSubjectId,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (application is null || !application.IsAdmissible)
                {
                    return false;
                }

                if (context.Operation == OrganizationJoinAdmissionOperation.ApproveEnrollment &&
                    (!context.ClaimId.HasValue || application.ClaimId != context.ClaimId))
                {
                    return false;
                }

                IAuthMemberContactReader contacts = services
                    .GetRequiredService<IAuthMemberContactReader>();
                string? verifiedEmail = await contacts.GetPreferredVerifiedEmailAsync(
                    options.Value.GlobalAuthScopeId,
                    memberId,
                    cancellationToken).ConfigureAwait(false);
                return !string.IsNullOrWhiteSpace(verifiedEmail) &&
                    string.Equals(
                        application.VerifiedAccountEmail,
                        verifiedEmail,
                        StringComparison.OrdinalIgnoreCase);
            }).ConfigureAwait(false);
    }
}
