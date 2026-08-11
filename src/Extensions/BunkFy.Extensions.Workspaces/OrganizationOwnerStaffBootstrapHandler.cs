namespace BunkFy.Extensions.Workspaces;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Messaging;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.Options;

[IntegrationEventHandler(HandlerName, RequiresExplicitProducerBinding = true)]
internal sealed class OrganizationOwnerStaffBootstrapHandler(
    IStaffIdentityBootstrapper staff,
    IAuthMemberContactReader contacts,
    IOrganizationAccessDecisionReader organizationAccess,
    IWorkspaceOperationalAdmissionPolicy operationalAdmission,
    IOptions<BunkFyWorkspacesOptions> options)
    : IIntegrationEventHandler<OrganizationMembershipChangedIntegrationEvent>
{
    public const string HandlerName = "bunkfy-workspace-staff-membership";

    public async Task HandleAsync(
        OrganizationMembershipChangedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        if (integrationEvent.Change != OrganizationMembershipChange.Joined ||
            integrationEvent.Status != OrganizationMembershipStatus.Active ||
            integrationEvent.Role != OrganizationMembershipRole.Owner)
        {
            return;
        }

        WorkspaceOperationalAdmissionDecision admission =
            await operationalAdmission.EvaluateAsync(
                integrationEvent.ScopeId,
                cancellationToken).ConfigureAwait(false);
        if (admission.Outcome != WorkspaceOperationalAdmissionOutcome.Allowed)
        {
            throw new InvalidOperationException(
                "Workspace operational admission did not allow owner Staff bootstrap.");
        }

        OrganizationAccessDecision access = await organizationAccess.ReadAsync(
            integrationEvent.OrganizationId,
            integrationEvent.SubjectId,
            cancellationToken).ConfigureAwait(false);
        if (access is OrganizationAccessDecision.OrganizationNotFound or
            OrganizationAccessDecision.OrganizationInactive or
            OrganizationAccessDecision.MembershipNotFound or
            OrganizationAccessDecision.MembershipInactive)
        {
            return;
        }

        if (access != OrganizationAccessDecision.Allowed)
        {
            throw new InvalidOperationException(
                "Organizations access is unavailable for owner Staff bootstrap.");
        }

        string? verifiedEmail = await this.GetVerifiedEmailAsync(
            integrationEvent.SubjectId,
            cancellationToken).ConfigureAwait(false);
        string displayName = verifiedEmail ?? DefaultDisplayName(integrationEvent.SubjectId);
        StaffIdentityBootstrapResult result = await staff.BootstrapAsync(
            new StaffIdentityBootstrapRequest(
                integrationEvent.EventId,
                integrationEvent.SubjectId,
                displayName,
                verifiedEmail,
                "integration:organizations"),
            cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Staff identity bootstrap failed with '{result.ErrorCode}'.");
        }
    }

    private async Task<string?> GetVerifiedEmailAsync(
        string subjectId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(subjectId, out Guid memberId))
        {
            return null;
        }

        return await contacts.GetPreferredVerifiedEmailAsync(
            options.Value.GlobalAuthScopeId,
            memberId,
            cancellationToken).ConfigureAwait(false);
    }

    private static string DefaultDisplayName(string subjectId)
    {
        const int visibleCharacters = 12;
        string suffix = subjectId.Length <= visibleCharacters
            ? subjectId
            : subjectId[..visibleCharacters];
        return $"Workspace member {suffix}";
    }
}
