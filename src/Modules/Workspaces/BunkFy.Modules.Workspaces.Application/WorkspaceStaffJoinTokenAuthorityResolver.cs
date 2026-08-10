namespace BunkFy.Modules.Workspaces.Application;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Modules.Organizations.Contracts;

internal readonly record struct WorkspaceStaffJoinTokenAuthority(
    Guid OrganizationId,
    Guid SourceId,
    bool AllowsSubmissionMutation);

internal sealed class WorkspaceStaffJoinTokenAuthorityResolver(
    IOrganizationJoinTokenInspector joinTokens)
{
    public async Task<WorkspaceStaffJoinTokenAuthority?> ResolveAsync(
        WorkspaceStaffOnboardingSourceKind sourceKind,
        string token,
        CancellationToken cancellationToken)
    {
        if (sourceKind == WorkspaceStaffOnboardingSourceKind.Invitation)
        {
            OrganizationJoinTokenInspection<OrganizationInvitationPreviewDto> inspected =
                await joinTokens.InspectInvitationAsync(token, cancellationToken).ConfigureAwait(false);
            return inspected.Preview is { Status: not OrganizationInvitationStatus.Unknown } preview
                ? new WorkspaceStaffJoinTokenAuthority(
                    preview.OrganizationId,
                    preview.InvitationId,
                    preview.Status == OrganizationInvitationStatus.Pending)
                : null;
        }

        if (sourceKind == WorkspaceStaffOnboardingSourceKind.EnrollmentLink)
        {
            OrganizationJoinTokenInspection<OrganizationEnrollmentPreviewDto> inspected =
                await joinTokens.InspectEnrollmentAsync(token, cancellationToken).ConfigureAwait(false);
            return inspected.Preview is { Status: not OrganizationEnrollmentLinkStatus.Unknown } preview
                ? new WorkspaceStaffJoinTokenAuthority(
                    preview.OrganizationId,
                    preview.EnrollmentLinkId,
                    preview.Status == OrganizationEnrollmentLinkStatus.Active)
                : null;
        }

        return null;
    }
}
