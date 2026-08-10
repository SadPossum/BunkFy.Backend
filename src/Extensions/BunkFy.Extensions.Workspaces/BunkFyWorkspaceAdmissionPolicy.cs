namespace BunkFy.Extensions.Workspaces;

using Gma.Modules.Auth.Contracts;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.Options;

internal sealed class BunkFyWorkspaceAdmissionPolicy(
    IAuthMemberAdmissionReader admissions,
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

        if (!Guid.TryParse(request.SubjectId, out Guid memberId))
        {
            return OrganizationCreationAdmissionDecision.SubjectVerificationRequired;
        }

        AuthMemberAdmission? admission = await admissions.FindActiveAsync(
                workspaceOptions.Value.GlobalAuthScopeId,
                memberId,
                cancellationToken)
            .ConfigureAwait(false);

        if (admission is null)
        {
            return OrganizationCreationAdmissionDecision.SubjectVerificationRequired;
        }

        return policy.RequireVerifiedEmailForWorkspaceCreation &&
            string.IsNullOrWhiteSpace(admission.PreferredVerifiedEmail)
            ? OrganizationCreationAdmissionDecision.SubjectVerificationRequired
            : OrganizationCreationAdmissionDecision.Allowed;
    }
}
