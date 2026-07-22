namespace BunkFy.Modules.Workspaces.Application;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Modules.Organizations.Contracts;

internal readonly record struct WorkspaceStaffJoinTokenAuthority(
    Guid OrganizationId,
    Guid SourceId);

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
            return inspected.Preview is { Status: OrganizationInvitationStatus.Pending } preview
                ? new WorkspaceStaffJoinTokenAuthority(preview.OrganizationId, preview.InvitationId)
                : null;
        }

        if (sourceKind == WorkspaceStaffOnboardingSourceKind.EnrollmentLink)
        {
            OrganizationJoinTokenInspection<OrganizationEnrollmentPreviewDto> inspected =
                await joinTokens.InspectEnrollmentAsync(token, cancellationToken).ConfigureAwait(false);
            return inspected.Preview is { Status: OrganizationEnrollmentLinkStatus.Active } preview
                ? new WorkspaceStaffJoinTokenAuthority(preview.OrganizationId, preview.EnrollmentLinkId)
                : null;
        }

        return null;
    }
}
