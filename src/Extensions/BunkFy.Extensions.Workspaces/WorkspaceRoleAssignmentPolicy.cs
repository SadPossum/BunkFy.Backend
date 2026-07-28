namespace BunkFy.Extensions.Workspaces;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Runtime.Time;
using Gma.Modules.AccessControl.Contracts;
using Microsoft.Extensions.Options;

internal sealed class WorkspaceRoleAssignmentPolicy(
    IOptions<BunkFySupportAccessOptions> options,
    ISystemClock clock) : IAccessRoleAssignmentPolicy
{
    public ValueTask<bool> IsAllowedAsync(
        AccessRoleAssignmentPolicyContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Subject.Kind != AccessSubjectKind.AdminActor)
        {
            return ValueTask.FromResult(true);
        }

        if (context.ExpiresAtUtc is null ||
            !WorkspaceAccessScopes.IsWorkspaceOrPropertyScope(context.AccessScope) ||
            !string.Equals(
                context.RoleName,
                WorkspaceAccessRoles.CompanySupport,
                StringComparison.Ordinal) ||
            context.Permissions.Any(permission =>
                !WorkspaceAccessRoles.CompanySupportPermissionCeiling.Contains(
                    permission,
                    StringComparer.Ordinal)))
        {
            return ValueTask.FromResult(false);
        }

        DateTimeOffset now = clock.UtcNow;
        DateTimeOffset maximum = now.AddMinutes(options.Value.MaximumGrantMinutes);
        return ValueTask.FromResult(
            context.ExpiresAtUtc > now &&
            context.ExpiresAtUtc <= maximum);
    }
}
