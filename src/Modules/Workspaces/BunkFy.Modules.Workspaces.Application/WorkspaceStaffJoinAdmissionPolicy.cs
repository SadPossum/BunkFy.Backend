namespace BunkFy.Modules.Workspaces.Application;

using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
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
    public async ValueTask<OrganizationJoinAdmissionDecision> EvaluateAsync(
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
            return OrganizationJoinAdmissionDecision.Denied;
        }

        return await authoritativeScope.RunAsync(
            context.OrganizationId,
            async services =>
            {
                WorkspaceOperationalAdmissionDecision operational =
                    await services
                        .GetRequiredService<
                            WorkspaceOperationalAdmissionEvaluator>()
                        .EvaluateAsync(
                            context.OrganizationId.ToString("D"),
                            cancellationToken)
                        .ConfigureAwait(false);
                if (operational.Outcome ==
                    WorkspaceOperationalAdmissionOutcome.Restricted)
                {
                    return OrganizationJoinAdmissionDecision.Denied;
                }

                if (operational.Outcome !=
                    WorkspaceOperationalAdmissionOutcome.Allowed)
                {
                    return OrganizationJoinAdmissionDecision.Unavailable;
                }

                IWorkspaceStaffAccessPlanRepository plans = services
                    .GetRequiredService<IWorkspaceStaffAccessPlanRepository>();
                WorkspaceStaffAccessPlan? plan = await plans.GetAsync(
                    context.SourceId,
                    cancellationToken).ConfigureAwait(false);
                if (plan is null || plan.SourceKind != sourceKind ||
                    plan.Status != WorkspaceStaffAccessPlanState.Active)
                {
                    return OrganizationJoinAdmissionDecision.Denied;
                }

                IWorkspaceStaffOnboardingRepository applications = services
                    .GetRequiredService<IWorkspaceStaffOnboardingRepository>();
                WorkspaceStaffOnboarding? application = await applications
                    .GetOperationalBySourceAndSubjectAsync(
                        sourceKind,
                        context.SourceId,
                        context.ApplicantSubjectId,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (application is null || !application.IsAdmissible)
                {
                    return OrganizationJoinAdmissionDecision.Denied;
                }

                if (context.Operation == OrganizationJoinAdmissionOperation.ApproveEnrollment &&
                    (!context.ClaimId.HasValue || application.ClaimId != context.ClaimId))
                {
                    return OrganizationJoinAdmissionDecision.Denied;
                }

                IAuthMemberContactReader contacts = services
                    .GetRequiredService<IAuthMemberContactReader>();
                string? verifiedEmail = await contacts.GetPreferredVerifiedEmailAsync(
                    options.Value.GlobalAuthScopeId,
                    memberId,
                    cancellationToken).ConfigureAwait(false);
                bool emailMatches = !string.IsNullOrWhiteSpace(verifiedEmail) &&
                    string.Equals(
                        application.VerifiedAccountEmail,
                        verifiedEmail,
                        StringComparison.OrdinalIgnoreCase);
                return emailMatches
                    ? OrganizationJoinAdmissionDecision.Allowed
                    : OrganizationJoinAdmissionDecision.Denied;
            }).ConfigureAwait(false);
    }
}
