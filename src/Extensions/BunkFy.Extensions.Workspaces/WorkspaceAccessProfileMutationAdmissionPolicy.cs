namespace BunkFy.Extensions.Workspaces;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.AccessControl;
using Gma.Modules.AccessControl.Contracts;

internal sealed class WorkspaceAccessProfileMutationAdmissionPolicy(
    IWorkspaceOperationalAdmissionPolicy operationalAdmission)
    : IAccessProfileMutationAdmissionPolicy
{
    private static readonly HashSet<string> ProtectedSeedKeys =
        WorkspaceAccessPermissionCatalogue.ProtectedSeedKeys.ToHashSet(
            StringComparer.Ordinal);

    public async ValueTask<AccessProfileMutationAdmissionDecision>
        EvaluateAsync(
            AccessProfileMutationAdmissionContext context,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!WorkspaceAccessScopes.IsWorkspaceOrPropertyScope(
                context.OwnerScope))
        {
            return AccessProfileMutationAdmissionDecision.Allowed;
        }

        if (context.ProfileKey is not null &&
            ProtectedSeedKeys.Contains(context.ProfileKey) &&
            !IsProvisioner(context.Actor))
        {
            return AccessProfileMutationAdmissionDecision.Denied;
        }

        WorkspaceOperationalAdmissionDecision decision =
            await operationalAdmission.EvaluateAsync(
                context.OwnerScope.Segments[0].Value,
                cancellationToken).ConfigureAwait(false);
        return decision.Outcome switch
        {
            WorkspaceOperationalAdmissionOutcome.Allowed =>
                AccessProfileMutationAdmissionDecision.Allowed,
            WorkspaceOperationalAdmissionOutcome.Restricted =>
                AccessProfileMutationAdmissionDecision.Denied,
            _ => AccessProfileMutationAdmissionDecision.Unavailable
        };
    }

    private static bool IsProvisioner(AccessSubject actor) =>
        actor.Kind == AccessSubjectKind.System &&
        string.Equals(
            actor.Id,
            WorkspaceAccessActors.Provisioner,
            StringComparison.Ordinal);
}
