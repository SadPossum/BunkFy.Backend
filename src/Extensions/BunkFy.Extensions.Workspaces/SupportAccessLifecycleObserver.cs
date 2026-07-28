namespace BunkFy.Extensions.Workspaces;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Observability;
using Gma.Modules.AccessControl.Contracts;

internal sealed class SupportAccessLifecycleObserver(
    ISecuritySignalRecorder securitySignals)
    : IAccessRoleAssignmentLifecycleObserver
{
    public ValueTask ObserveAsync(
        AccessRoleAssignmentLifecycleEvent lifecycleEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lifecycleEvent);
        if (lifecycleEvent.Subject.Kind != AccessSubjectKind.AdminActor ||
            !string.Equals(
                lifecycleEvent.RoleName,
                WorkspaceAccessRoles.CompanySupport,
                StringComparison.Ordinal))
        {
            return ValueTask.CompletedTask;
        }

        SecuritySignalDefinition? definition = lifecycleEvent.Stage switch
        {
            AccessRoleAssignmentLifecycleStage.Requested =>
                SupportAccessSecuritySignalDefinitions.Requested,
            AccessRoleAssignmentLifecycleStage.Granted
                when lifecycleEvent.ExpiresAtUtc is not null &&
                     WorkspaceAccessScopes.IsWorkspaceOrPropertyScope(lifecycleEvent.AccessScope) =>
                SupportAccessSecuritySignalDefinitions.Granted,
            AccessRoleAssignmentLifecycleStage.Denied =>
                SupportAccessSecuritySignalDefinitions.Denied,
            AccessRoleAssignmentLifecycleStage.Revoked =>
                SupportAccessSecuritySignalDefinitions.Revoked,
            _ => null
        };

        if (definition is not null)
        {
            securitySignals.Record(definition, lifecycleEvent.CorrelationId);
        }

        return ValueTask.CompletedTask;
    }
}
