namespace BunkFy.Extensions.Workspaces;

using Gma.Modules.Auth.Contracts;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.Options;

internal sealed class BunkFyWorkspaceAdmissionPolicy(
    IAuthMemberContactReader contacts,
    IOptions<BunkFyWorkspacesOptions> workspaceOptions,
    IOptions<BunkFyWorkspaceAdmissionOptions> admissionOptions)
    : IOrganizationCreationAdmissionPolicy
{
    public async ValueTask<OrganizationCreationAdmissionDecision> EvaluateAsync(
        OrganizationCreationAdmissionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        BunkFyWorkspaceAdmissionOptions policy = admissionOptions.Value;
        if (policy.WorkspaceCreation != BunkFyWorkspaceCreationMode.SelfService)
        {
            return OrganizationCreationAdmissionDecision.Denied;
        }

        if (!policy.RequireVerifiedEmailForWorkspaceCreation)
        {
            return OrganizationCreationAdmissionDecision.Allowed;
        }

        if (!Guid.TryParse(request.SubjectId, out Guid memberId))
        {
            return OrganizationCreationAdmissionDecision.SubjectVerificationRequired;
        }

        string? verifiedEmail = await contacts.GetPreferredVerifiedEmailAsync(
            workspaceOptions.Value.GlobalAuthScopeId,
            memberId,
            cancellationToken).ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(verifiedEmail)
            ? OrganizationCreationAdmissionDecision.SubjectVerificationRequired
            : OrganizationCreationAdmissionDecision.Allowed;
    }
}
