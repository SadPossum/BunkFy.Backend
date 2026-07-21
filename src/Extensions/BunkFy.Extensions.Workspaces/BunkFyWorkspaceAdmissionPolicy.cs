namespace BunkFy.Extensions.Workspaces;

using Gma.Framework.Results;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Application.Ports;
using Microsoft.Extensions.Options;

internal sealed class BunkFyWorkspaceAdmissionPolicy(
    IAuthMemberContactReader contacts,
    IOptions<BunkFyWorkspacesOptions> workspaceOptions,
    IOptions<BunkFyWorkspaceAdmissionOptions> admissionOptions)
    : IOrganizationAdmissionPolicy
{
    public async Task<Result> CanCreateOrganizationAsync(
        string subjectId,
        CancellationToken cancellationToken)
    {
        BunkFyWorkspaceAdmissionOptions policy = admissionOptions.Value;
        if (policy.WorkspaceCreation != BunkFyWorkspaceCreationMode.SelfService)
        {
            return Result.Failure(OrganizationApplicationErrors.SelfServiceCreationDisabled);
        }

        if (!policy.RequireVerifiedEmailForWorkspaceCreation)
        {
            return Result.Success();
        }

        if (!Guid.TryParse(subjectId, out Guid memberId))
        {
            return Result.Failure(OrganizationApplicationErrors.SubjectVerificationRequired);
        }

        string? verifiedEmail = await contacts.GetPreferredVerifiedEmailAsync(
            workspaceOptions.Value.GlobalAuthScopeId,
            memberId,
            cancellationToken).ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(verifiedEmail)
            ? Result.Failure(OrganizationApplicationErrors.SubjectVerificationRequired)
            : Result.Success();
    }
}
