namespace BunkFy.Modules.Workspaces.Application;

using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Permissions;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Gma.Modules.AccessControl.Contracts;

public sealed record WorkspaceMemberAccessUpdate(
    Guid ProfileId,
    IReadOnlyCollection<Guid>? PropertyIds);

public interface IWorkspaceMemberAccessManager
{
    Task<Result<WorkspaceMemberAccessDto>> GetAsync(
        string subjectId,
        AccessSubject actor,
        CancellationToken cancellationToken = default);

    Task<Result<WorkspaceMemberAccessDto>> UpdateAsync(
        string subjectId,
        WorkspaceMemberAccessUpdate update,
        AccessSubject actor,
        CancellationToken cancellationToken = default);
}

internal sealed class WorkspaceMemberAccessManager(
    IAccessControlRoleProvisioner roles,
    IAccessProfileManager profiles,
    IScopedAccessProfileManager assignments,
    IWorkspacePropertyProjectionRepository properties,
    IAccessAuthorizationService authorization,
    IScopeContext scopeContext) : IWorkspaceMemberAccessManager
{
    private const int MaximumPropertyCount = 250;
    private static readonly HashSet<string> ProductPermissions =
        WorkspaceAccessPermissionCatalogue.All
            .Select(permission => permission.Code)
            .ToHashSet(StringComparer.Ordinal);

    public async Task<Result<WorkspaceMemberAccessDto>> GetAsync(
        string subjectId,
        AccessSubject actor,
        CancellationToken cancellationToken = default)
    {
        Result<(AccessScope Scope, AccessSubject Subject)> context = this.GetContext(subjectId);
        if (context.IsFailure)
        {
            return Result.Failure<WorkspaceMemberAccessDto>(context.Error);
        }

        Result<ScopedAccessProfileAssignmentSet> assigned = await assignments.GetSubjectAssignmentsAsync(
            context.Value.Subject,
            context.Value.Scope,
            actor,
            cancellationToken).ConfigureAwait(false);
        if (assigned.IsFailure)
        {
            return Result.Failure<WorkspaceMemberAccessDto>(assigned.Error);
        }

        Result member = await this.ValidateOrdinaryMemberAsync(
            context.Value.Subject,
            context.Value.Scope,
            cancellationToken).ConfigureAwait(false);
        return member.IsFailure
            ? Result.Failure<WorkspaceMemberAccessDto>(member.Error)
            : Result.Success(ToProduct(context.Value.Subject, assigned.Value.Assignments));
    }

    public async Task<Result<WorkspaceMemberAccessDto>> UpdateAsync(
        string subjectId,
        WorkspaceMemberAccessUpdate update,
        AccessSubject actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        Result<(AccessScope Scope, AccessSubject Subject)> context = this.GetContext(subjectId);
        if (context.IsFailure)
        {
            return Result.Failure<WorkspaceMemberAccessDto>(context.Error);
        }

        Result access = await this.AuthorizeAssignAsync(
            actor,
            context.Value.Scope,
            cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return Result.Failure<WorkspaceMemberAccessDto>(access.Error);
        }

        Result member = await this.ValidateOrdinaryMemberAsync(
            context.Value.Subject,
            context.Value.Scope,
            cancellationToken).ConfigureAwait(false);
        if (member.IsFailure)
        {
            return Result.Failure<WorkspaceMemberAccessDto>(member.Error);
        }

        if (update.ProfileId == Guid.Empty)
        {
            return Result.Failure<WorkspaceMemberAccessDto>(
                WorkspaceAccessManagementErrors.ProfileUnavailable);
        }

        Result<AccessProfileDto> profile = await profiles.GetProfileAsync(
            update.ProfileId,
            context.Value.Scope,
            actor,
            cancellationToken).ConfigureAwait(false);
        if (profile.IsFailure)
        {
            return Result.Failure<WorkspaceMemberAccessDto>(profile.Error);
        }

        if (profile.Value.Status != AccessProfileStatus.Active ||
            profile.Value.Permissions.Any(permission => !ProductPermissions.Contains(permission)))
        {
            return Result.Failure<WorkspaceMemberAccessDto>(
                WorkspaceAccessManagementErrors.ProfileUnavailable);
        }

        Result<Guid[]> propertyIds = await this.ValidatePropertiesAsync(
            update.PropertyIds,
            cancellationToken).ConfigureAwait(false);
        if (propertyIds.IsFailure)
        {
            return Result.Failure<WorkspaceMemberAccessDto>(propertyIds.Error);
        }

        AccessProfileAssignmentTarget[] targets = propertyIds.Value.Length == 0
            ? [new AccessProfileAssignmentTarget(profile.Value.Id, context.Value.Scope)]
            : propertyIds.Value.Select(propertyId => new AccessProfileAssignmentTarget(
                profile.Value.Id,
                WorkspaceAccessScopes.CreateProperty(scopeContext.ScopeId!, propertyId))).ToArray();
        Result<ScopedAccessProfileAssignmentReconciliation> reconciled = await assignments
            .ReconcileSubjectAssignmentsAsync(
                context.Value.Subject,
                context.Value.Scope,
                targets,
                actor,
                cancellationToken).ConfigureAwait(false);
        return reconciled.IsFailure
            ? Result.Failure<WorkspaceMemberAccessDto>(reconciled.Error)
            : Result.Success(ToProduct(context.Value.Subject, profile.Value, targets));
    }

    private Result<(AccessScope Scope, AccessSubject Subject)> GetContext(string subjectId)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<(AccessScope, AccessSubject)>(
                WorkspaceAccessManagementErrors.ScopeRequired);
        }

        string candidate = subjectId?.Trim() ?? string.Empty;
        if (candidate.Length == 0)
        {
            return Result.Failure<(AccessScope, AccessSubject)>(
                WorkspaceAccessManagementErrors.MemberInvalid);
        }

        return Result.Success((
            WorkspaceAccessScopes.Create(scopeContext.ScopeId),
            AccessSubject.User(candidate)));
    }

    private async Task<Result> ValidateOrdinaryMemberAsync(
        AccessSubject subject,
        AccessScope scope,
        CancellationToken cancellationToken)
    {
        if (await roles.HasAssignmentAsync(
            subject,
            WorkspaceAccessRoles.Owner,
            scope,
            cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(WorkspaceAccessManagementErrors.OwnerProtected);
        }

        bool member = await roles.HasAssignmentAsync(
            subject,
            WorkspaceAccessRoles.MembershipMarker,
            scope,
            cancellationToken).ConfigureAwait(false);
        if (!member)
        {
            member = await roles.HasAssignmentAsync(
                subject,
                WorkspaceAccessRoles.LegacyMember,
                scope,
                cancellationToken).ConfigureAwait(false);
        }

        return member
            ? Result.Success()
            : Result.Failure(WorkspaceAccessManagementErrors.MemberUnavailable);
    }

    private async Task<Result<Guid[]>> ValidatePropertiesAsync(
        IReadOnlyCollection<Guid>? requested,
        CancellationToken cancellationToken)
    {
        if (requested is null || requested.Count > MaximumPropertyCount ||
            requested.Any(propertyId => propertyId == Guid.Empty) ||
            requested.Count != requested.Distinct().Count())
        {
            return Result.Failure<Guid[]>(WorkspaceAccessManagementErrors.PropertiesInvalid);
        }

        Guid[] normalized = requested.Order().ToArray();
        return normalized.Length == 0 || await properties.AreAllActiveAsync(
            normalized,
            cancellationToken).ConfigureAwait(false)
            ? Result.Success(normalized)
            : Result.Failure<Guid[]>(WorkspaceAccessManagementErrors.PropertyUnavailable);
    }

    private async Task<Result> AuthorizeAssignAsync(
        AccessSubject actor,
        AccessScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        AccessDecision decision = await authorization.AuthorizeAsync(
            new AccessRequirement(
                actor,
                PermissionCode.Create(AccessControlProfilePermissionCodes.Assign),
                scope),
            cancellationToken).ConfigureAwait(false);
        return decision.IsAllowed
            ? Result.Success()
            : Result.Failure(ScopedAccessProfileManagementErrors.AccessDenied);
    }

    private static WorkspaceMemberAccessDto ToProduct(
        AccessSubject subject,
        IReadOnlyList<ScopedAccessProfileAssignment> assigned) => new(
            subject.Id,
            assigned.Select(assignment => new WorkspaceMemberAccessAssignmentDto(
                assignment.Profile.Id,
                assignment.Profile.Key,
                assignment.Profile.DisplayName,
                assignment.Profile.Version,
                GetPropertyId(assignment.AssignmentScope))).ToArray());

    private static WorkspaceMemberAccessDto ToProduct(
        AccessSubject subject,
        AccessProfileDto profile,
        IReadOnlyList<AccessProfileAssignmentTarget> targets) => new(
            subject.Id,
            targets.Select(target => new WorkspaceMemberAccessAssignmentDto(
                profile.Id,
                profile.Key,
                profile.DisplayName,
                profile.Version,
                GetPropertyId(target.AssignmentScope))).ToArray());

    private static Guid? GetPropertyId(AccessScope scope) =>
        scope.Segments.Count == 2 &&
        string.Equals(
            scope.Segments[1].Name,
            WorkspaceAccessScopes.PropertySegmentName,
            StringComparison.Ordinal) &&
        Guid.TryParse(scope.Segments[1].Value, out Guid propertyId)
            ? propertyId
            : null;
}
