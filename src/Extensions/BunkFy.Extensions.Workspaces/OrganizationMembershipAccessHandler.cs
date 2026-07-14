namespace BunkFy.Extensions.Workspaces;

using Gma.Framework.AccessControl;
using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Time;
using Gma.Modules.AccessControl.Application.Ports;
using Gma.Modules.Organizations.Contracts;

[IntegrationEventHandler(HandlerName, RequiresExplicitProducerBinding = true)]
internal sealed class OrganizationMembershipAccessHandler(
    IAccessControlRbacRepository accessControl,
    ISystemClock clock)
    : IIntegrationEventHandler<OrganizationMembershipChangedIntegrationEvent>
{
    public const string HandlerName = "bunkfy-workspace-access-membership";

    public async Task HandleAsync(
        OrganizationMembershipChangedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        DateTimeOffset nowUtc = clock.UtcNow;
        AccessSubject subject = AccessSubject.User(integrationEvent.SubjectId);
        AccessScope scope = AccessScope.Create(
            AccessScopeSegment.Create("tenant", integrationEvent.ScopeId));

        await this.EnsureRoleAsync(
            WorkspaceAccessRoles.Owner,
            WorkspaceAccessRoles.OwnerPermissions,
            nowUtc,
            cancellationToken).ConfigureAwait(false);
        await this.EnsureRoleAsync(
            WorkspaceAccessRoles.Member,
            WorkspaceAccessRoles.MemberPermissions,
            nowUtc,
            cancellationToken).ConfigureAwait(false);
        await accessControl.EnsureSubjectAsync(subject, nowUtc, cancellationToken).ConfigureAwait(false);

        if (integrationEvent.Status != OrganizationMembershipStatus.Active)
        {
            await this.RemoveAsync(subject, WorkspaceAccessRoles.Owner, scope, cancellationToken).ConfigureAwait(false);
            await this.RemoveAsync(subject, WorkspaceAccessRoles.Member, scope, cancellationToken).ConfigureAwait(false);
            return;
        }

        bool isOwner = integrationEvent.Role == OrganizationMembershipRole.Owner;
        string desiredRole = isOwner ? WorkspaceAccessRoles.Owner : WorkspaceAccessRoles.Member;
        string obsoleteRole = isOwner ? WorkspaceAccessRoles.Member : WorkspaceAccessRoles.Owner;
        await accessControl.EnsureRoleAssignmentAsync(
            subject,
            desiredRole,
            scope,
            nowUtc,
            cancellationToken).ConfigureAwait(false);
        await this.RemoveAsync(subject, obsoleteRole, scope, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureRoleAsync(
        string roleName,
        IReadOnlyList<string> permissions,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        await accessControl.EnsureRoleAsync(roleName, nowUtc, cancellationToken).ConfigureAwait(false);
        foreach (string permission in permissions)
        {
            await accessControl.EnsureRolePermissionAsync(
                roleName,
                permission,
                nowUtc,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RemoveAsync(
        AccessSubject subject,
        string roleName,
        AccessScope scope,
        CancellationToken cancellationToken)
    {
        AccessControlRemovalOutcome outcome = await accessControl.UnassignRoleAsync(
            subject,
            roleName,
            scope,
            cancellationToken).ConfigureAwait(false);
        if (outcome == AccessControlRemovalOutcome.LastOwnerProtected)
        {
            throw new InvalidOperationException("AccessControl protected the final owner assignment.");
        }
    }
}
