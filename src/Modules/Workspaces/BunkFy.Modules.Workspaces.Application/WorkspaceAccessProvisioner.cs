namespace BunkFy.Modules.Workspaces.Application;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.AccessControl;
using Gma.Modules.AccessControl.Contracts;

internal sealed class WorkspaceAccessProvisioner(
    IAccessControlRoleProvisioner roles,
    IAccessProfileProvisioner profiles)
{
    internal const string ProvisioningActorId = "bunkfy-workspace-access-provisioner";
    private const int AssignmentPageSize = 100;

    public async Task ProvisionDefaultMemberAsync(
        string workspaceId,
        string subjectId,
        CancellationToken cancellationToken)
    {
        AccessScope scope = WorkspaceAccessScopes.Create(workspaceId);
        AccessSubject subject = AccessSubject.User(subjectId);
        AccessSubject actor = AccessSubject.System(ProvisioningActorId);

        await this.EnsureMembershipMarkerAsync(cancellationToken).ConfigureAwait(false);
        AccessProfileDto frontDesk = await profiles.EnsureProfileAsync(
                scope,
                WorkspaceAccessProfileSeeds.FrontDesk,
                actor,
                cancellationToken)
            .ConfigureAwait(false);
        if (frontDesk.Status != AccessProfileStatus.Active)
        {
            throw new InvalidOperationException("The Front desk access profile is not active.");
        }

        await this.ProvisionDefaultMemberAsync(
                subject,
                scope,
                frontDesk.Id,
                actor,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ProvisionDefaultMemberAsync(
        AccessSubject subject,
        AccessScope scope,
        Guid frontDeskProfileId,
        AccessSubject actor,
        CancellationToken cancellationToken)
    {
        await roles.EnsureAssignmentAsync(
                subject,
                WorkspaceAccessRoles.MembershipMarker,
                scope,
                cancellationToken)
            .ConfigureAwait(false);

        AccessProfileAssignmentSet current = await profiles.GetSubjectAssignmentsAsync(
                subject,
                scope,
                cancellationToken)
            .ConfigureAwait(false);
        Guid[] desiredProfileIds = current.Profiles
            .Select(profile => profile.Id)
            .Append(frontDeskProfileId)
            .Distinct()
            .ToArray();

        await profiles.ReconcileSubjectAssignmentsAsync(
                subject,
                scope,
                desiredProfileIds,
                actor,
                cancellationToken)
            .ConfigureAwait(false);

        await this.RemoveLegacyMemberAssignmentAsync(subject, scope, cancellationToken).ConfigureAwait(false);
    }

    public async Task<WorkspaceAccessBootstrapResult> BackfillLegacyMembersAsync(
        string workspaceId,
        CancellationToken cancellationToken)
    {
        AccessScope scope = WorkspaceAccessScopes.Create(workspaceId);
        AccessSubject actor = AccessSubject.System(ProvisioningActorId);

        await this.EnsureMembershipMarkerAsync(cancellationToken).ConfigureAwait(false);
        AccessProfileDto? frontDesk = null;
        foreach (AccessProfileDefinition seed in WorkspaceAccessProfileSeeds.All)
        {
            AccessProfileDto ensured = await profiles.EnsureProfileAsync(
                    scope,
                    seed,
                    actor,
                    cancellationToken)
                .ConfigureAwait(false);
            if (string.Equals(seed.Key, WorkspaceAccessProfileSeeds.FrontDeskKey, StringComparison.Ordinal))
            {
                frontDesk = ensured;
            }

            if (ensured.Status != AccessProfileStatus.Active)
            {
                throw new InvalidOperationException(
                    $"The workspace access profile seed '{seed.Key}' is not active.");
            }
        }

        if (frontDesk is null)
        {
            throw new InvalidOperationException("The Front desk access profile seed is missing.");
        }

        int migratedMemberCount = 0;
        while (true)
        {
            AccessControlPage<AccessControlRoleAssignment> page = await roles.ListAssignmentsAsync(
                    WorkspaceAccessRoles.LegacyMember,
                    scope,
                    page: 1,
                    pageSize: AssignmentPageSize,
                    cancellationToken)
                .ConfigureAwait(false);
            if (page.Items.Count == 0)
            {
                break;
            }

            foreach (AccessControlRoleAssignment assignment in page.Items)
            {
                if (assignment.SubjectKind != AccessSubjectKind.User)
                {
                    throw new InvalidOperationException(
                        "The legacy workspace member role contains a non-user assignment.");
                }

                await this.ProvisionDefaultMemberAsync(
                        AccessSubject.User(assignment.SubjectId),
                        scope,
                        frontDesk.Id,
                        actor,
                        cancellationToken)
                    .ConfigureAwait(false);
                migratedMemberCount++;
            }
        }

        return new WorkspaceAccessBootstrapResult(
            WorkspaceAccessProfileSeeds.Version,
            WorkspaceAccessProfileSeeds.All.Count,
            migratedMemberCount);
    }

    public async Task EnsureSeedProfilesAsync(
        string workspaceId,
        CancellationToken cancellationToken)
    {
        AccessScope scope = WorkspaceAccessScopes.Create(workspaceId);
        AccessSubject actor = AccessSubject.System(ProvisioningActorId);
        foreach (AccessProfileDefinition seed in WorkspaceAccessProfileSeeds.All)
        {
            await profiles.EnsureProfileAsync(scope, seed, actor, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyCollection<Guid>> CaptureRestorableProfileIdsAsync(
        string workspaceId,
        string subjectId,
        CancellationToken cancellationToken)
    {
        AccessScope scope = WorkspaceAccessScopes.Create(workspaceId);
        AccessSubject subject = AccessSubject.User(subjectId);
        if (await roles.HasAssignmentAsync(
                subject,
                WorkspaceAccessRoles.LegacyMember,
                scope,
                cancellationToken).ConfigureAwait(false))
        {
            await this.ProvisionDefaultMemberAsync(
                workspaceId,
                subjectId,
                cancellationToken).ConfigureAwait(false);
        }

        AccessProfileAssignmentSet assignments = await profiles.GetSubjectAssignmentsAsync(
            subject,
            scope,
            cancellationToken).ConfigureAwait(false);
        return assignments.Profiles.Select(profile => profile.Id).Distinct().ToArray();
    }

    public async Task DenyMemberAsync(
        string workspaceId,
        string subjectId,
        CancellationToken cancellationToken)
    {
        AccessScope scope = WorkspaceAccessScopes.Create(workspaceId);
        AccessSubject subject = AccessSubject.User(subjectId);
        AccessSubject actor = AccessSubject.System(ProvisioningActorId);
        await profiles.ReconcileSubjectAssignmentsAsync(
            subject,
            scope,
            [],
            actor,
            cancellationToken).ConfigureAwait(false);
        await this.RemoveAssignmentAsync(
            subject, WorkspaceAccessRoles.MembershipMarker, scope, cancellationToken).ConfigureAwait(false);
        await this.RemoveAssignmentAsync(
            subject, WorkspaceAccessRoles.LegacyMember, scope, cancellationToken).ConfigureAwait(false);
    }

    public async Task RestoreMemberAsync(
        string workspaceId,
        string subjectId,
        IReadOnlyCollection<Guid> profileIds,
        CancellationToken cancellationToken)
    {
        AccessScope scope = WorkspaceAccessScopes.Create(workspaceId);
        AccessSubject subject = AccessSubject.User(subjectId);
        AccessSubject actor = AccessSubject.System(ProvisioningActorId);
        await this.EnsureMembershipMarkerAsync(cancellationToken).ConfigureAwait(false);
        await roles.EnsureAssignmentAsync(
            subject,
            WorkspaceAccessRoles.MembershipMarker,
            scope,
            cancellationToken).ConfigureAwait(false);
        await profiles.ReconcileSubjectAssignmentsAsync(
            subject,
            scope,
            profileIds,
            actor,
            cancellationToken).ConfigureAwait(false);
        await this.RemoveAssignmentAsync(
            subject, WorkspaceAccessRoles.LegacyMember, scope, cancellationToken).ConfigureAwait(false);
    }

    public async Task<WorkspaceAccessBootstrapStatus> InspectAsync(
        string workspaceId,
        CancellationToken cancellationToken)
    {
        AccessScope scope = WorkspaceAccessScopes.Create(workspaceId);
        int activeSeedProfileCount = 0;
        int archivedSeedProfileCount = 0;
        foreach (AccessProfileDefinition seed in WorkspaceAccessProfileSeeds.All)
        {
            AccessProfileDto? profile = await profiles.FindProfileByKeyAsync(
                    scope,
                    seed.Key,
                    cancellationToken)
                .ConfigureAwait(false);
            if (profile?.Status == AccessProfileStatus.Active)
            {
                activeSeedProfileCount++;
            }
            else if (profile?.Status == AccessProfileStatus.Archived)
            {
                archivedSeedProfileCount++;
            }
        }

        int legacyMemberCount = await this.CountAssignmentsAsync(
                WorkspaceAccessRoles.LegacyMember,
                scope,
                cancellationToken)
            .ConfigureAwait(false);
        int markerMemberCount = await this.CountAssignmentsAsync(
                WorkspaceAccessRoles.MembershipMarker,
                scope,
                cancellationToken)
            .ConfigureAwait(false);

        return new WorkspaceAccessBootstrapStatus(
            WorkspaceAccessProfileSeeds.Version,
            WorkspaceAccessProfileSeeds.All.Count,
            activeSeedProfileCount,
            archivedSeedProfileCount,
            legacyMemberCount,
            markerMemberCount);
    }

    private Task EnsureMembershipMarkerAsync(CancellationToken cancellationToken) =>
        roles.EnsureRoleAsync(
            new AccessControlRoleDefinition(
                WorkspaceAccessRoles.MembershipMarker,
                WorkspaceAccessRoles.MembershipMarkerPermissions),
            cancellationToken);

    private async Task RemoveLegacyMemberAssignmentAsync(
        AccessSubject subject,
        AccessScope scope,
        CancellationToken cancellationToken)
    {
        await this.RemoveAssignmentAsync(
            subject,
            WorkspaceAccessRoles.LegacyMember,
            scope,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task RemoveAssignmentAsync(
        AccessSubject subject,
        string roleName,
        AccessScope scope,
        CancellationToken cancellationToken)
    {
        AccessControlAssignmentRemovalOutcome outcome = await roles.RemoveAssignmentAsync(
            subject,
            roleName,
            scope,
            cancellationToken).ConfigureAwait(false);
        if (outcome is AccessControlAssignmentRemovalOutcome.Unknown or
            AccessControlAssignmentRemovalOutcome.LastOwnerProtected)
        {
            throw new InvalidOperationException("The workspace access assignment could not be removed.");
        }
    }

    private async Task<int> CountAssignmentsAsync(
        string roleName,
        AccessScope scope,
        CancellationToken cancellationToken)
    {
        int count = 0;
        int pageNumber = 1;
        while (true)
        {
            AccessControlPage<AccessControlRoleAssignment> page = await roles.ListAssignmentsAsync(
                    roleName,
                    scope,
                    pageNumber,
                    AssignmentPageSize,
                    cancellationToken)
                .ConfigureAwait(false);
            count += page.Items.Count;
            if (!page.HasMore)
            {
                return count;
            }

            pageNumber++;
        }
    }
}
