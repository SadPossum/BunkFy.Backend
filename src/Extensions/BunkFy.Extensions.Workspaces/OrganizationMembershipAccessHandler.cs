namespace BunkFy.Extensions.Workspaces;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Messaging;
using Gma.Modules.AccessControl.Contracts;
using Gma.Modules.Organizations.Contracts;

[IntegrationEventHandler(HandlerName, RequiresExplicitProducerBinding = true)]
internal sealed class OrganizationMembershipAccessHandler(
    IAccessControlRoleProvisioner accessControl)
    : IIntegrationEventHandler<OrganizationMembershipChangedIntegrationEvent>
{
    public const string HandlerName = "bunkfy-workspace-access-membership";

    public async Task HandleAsync(
        OrganizationMembershipChangedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        if (integrationEvent.Status != OrganizationMembershipStatus.Active)
        {
            return;
        }

        AccessSubject subject = AccessSubject.User(integrationEvent.SubjectId);
        AccessScope scope = WorkspaceAccessScopes.Create(integrationEvent.ScopeId);

        await accessControl.EnsureRoleAsync(
            new AccessControlRoleDefinition(
                WorkspaceAccessRoles.Owner,
                WorkspaceAccessRoles.OwnerPermissions),
            cancellationToken).ConfigureAwait(false);
        if (integrationEvent.Role == OrganizationMembershipRole.Owner)
        {
            await accessControl.EnsureAssignmentAsync(
                subject,
                WorkspaceAccessRoles.Owner,
                scope,
                cancellationToken).ConfigureAwait(false);
            await this.RemoveAsync(
                subject,
                WorkspaceAccessRoles.MembershipMarker,
                scope,
                cancellationToken).ConfigureAwait(false);
            await this.RemoveAsync(
                subject,
                WorkspaceAccessRoles.LegacyMember,
                scope,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (integrationEvent.Change != OrganizationMembershipChange.DemotedToMember)
        {
            return;
        }

        await this.RemoveAsync(
            subject,
            WorkspaceAccessRoles.Owner,
            scope,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task RemoveAsync(
        AccessSubject subject,
        string roleName,
        AccessScope scope,
        CancellationToken cancellationToken)
    {
        AccessControlAssignmentRemovalOutcome outcome = await accessControl.RemoveAssignmentAsync(
            subject,
            roleName,
            scope,
            cancellationToken).ConfigureAwait(false);
        if (outcome == AccessControlAssignmentRemovalOutcome.LastOwnerProtected)
        {
            throw new InvalidOperationException("AccessControl protected the final owner assignment.");
        }
    }
}
