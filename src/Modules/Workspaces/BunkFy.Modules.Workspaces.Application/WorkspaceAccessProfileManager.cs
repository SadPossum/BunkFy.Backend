namespace BunkFy.Modules.Workspaces.Application;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Permissions;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Gma.Modules.AccessControl.Contracts;

public sealed record WorkspaceAccessProfileCreation(
    Guid RequestId,
    string DisplayName,
    string? Description,
    IReadOnlyCollection<string>? Permissions);

public sealed record WorkspaceAccessProfileUpdate(
    string DisplayName,
    string? Description,
    IReadOnlyCollection<string>? Permissions,
    long ExpectedVersion);

public interface IWorkspaceAccessProfileManager
{
    Task<Result<WorkspaceAccessCatalogueDto>> GetCatalogueAsync(
        AccessSubject actor,
        CancellationToken cancellationToken = default);

    Task<Result<WorkspaceAccessProfileListResponse>> ListProfilesAsync(
        bool includeArchived,
        int page,
        int pageSize,
        AccessSubject actor,
        CancellationToken cancellationToken = default);

    Task<Result<WorkspaceAccessProfileDto>> CreateProfileAsync(
        WorkspaceAccessProfileCreation request,
        AccessSubject actor,
        CancellationToken cancellationToken = default);

    Task<Result<WorkspaceAccessProfileDto>> UpdateProfileAsync(
        Guid profileId,
        WorkspaceAccessProfileUpdate request,
        AccessSubject actor,
        CancellationToken cancellationToken = default);

    Task<Result> ArchiveProfileAsync(
        Guid profileId,
        long expectedVersion,
        AccessSubject actor,
        CancellationToken cancellationToken = default);
}

internal sealed class WorkspaceAccessProfileManager(
    IAccessProfileManager profiles,
    IAccessProfileProvisioner profileReader,
    IAccessAuthorizationService authorization,
    IScopeContext scopeContext) : IWorkspaceAccessProfileManager
{
    private static readonly Dictionary<string, WorkspaceAccessPermissionDto> Catalogue =
        WorkspaceAccessPermissionCatalogue.All.ToDictionary(item => item.Code, StringComparer.Ordinal);
    private static readonly HashSet<string> ProtectedSeedKeys =
        WorkspaceAccessPermissionCatalogue.ProtectedSeedKeys.ToHashSet(StringComparer.Ordinal);

    public async Task<Result<WorkspaceAccessCatalogueDto>> GetCatalogueAsync(
        AccessSubject actor,
        CancellationToken cancellationToken = default)
    {
        Result<AccessScope> scope = this.GetScope();
        if (scope.IsFailure)
        {
            return Result.Failure<WorkspaceAccessCatalogueDto>(scope.Error);
        }

        Result<IReadOnlyList<string>> allowed = await profiles.ListAllowedPermissionsAsync(
            scope.Value,
            actor,
            cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure)
        {
            return Result.Failure<WorkspaceAccessCatalogueDto>(allowed.Error);
        }

        HashSet<string> available = allowed.Value.ToHashSet(StringComparer.Ordinal);
        WorkspaceAccessPermissionDto[] permissions = WorkspaceAccessPermissionCatalogue.All
            .Where(permission => available.Contains(permission.Code) &&
                permission.RequiredPermissions.All(available.Contains))
            .ToArray();
        return Result.Success(new WorkspaceAccessCatalogueDto(
            permissions,
            WorkspaceAccessPermissionCatalogue.ProtectedSeedKeys));
    }

    public async Task<Result<WorkspaceAccessProfileListResponse>> ListProfilesAsync(
        bool includeArchived,
        int page,
        int pageSize,
        AccessSubject actor,
        CancellationToken cancellationToken = default)
    {
        Result<AccessScope> scope = this.GetScope();
        if (scope.IsFailure)
        {
            return Result.Failure<WorkspaceAccessProfileListResponse>(scope.Error);
        }

        Result<AccessControlPage<AccessProfileDto>> listed = await profiles.ListProfilesAsync(
            scope.Value,
            includeArchived,
            page,
            pageSize,
            actor,
            cancellationToken).ConfigureAwait(false);
        return listed.IsFailure
            ? Result.Failure<WorkspaceAccessProfileListResponse>(listed.Error)
            : Result.Success(new WorkspaceAccessProfileListResponse(
                listed.Value.Items.Select(ToProduct).ToArray(),
                listed.Value.Page,
                listed.Value.PageSize,
                listed.Value.HasMore));
    }

    public async Task<Result<WorkspaceAccessProfileDto>> CreateProfileAsync(
        WorkspaceAccessProfileCreation request,
        AccessSubject actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Result<AccessScope> scope = this.GetScope();
        if (scope.IsFailure)
        {
            return Result.Failure<WorkspaceAccessProfileDto>(scope.Error);
        }

        Result access = await this.AuthorizeManageAsync(
            actor,
            scope.Value,
            cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return Result.Failure<WorkspaceAccessProfileDto>(access.Error);
        }

        if (request.RequestId == Guid.Empty)
        {
            return Result.Failure<WorkspaceAccessProfileDto>(
                WorkspaceAccessManagementErrors.RequestInvalid);
        }

        Result<IReadOnlyList<string>> permissions = await this.ValidatePermissionsAsync(
            request.Permissions,
            scope.Value,
            actor,
            cancellationToken).ConfigureAwait(false);
        if (permissions.IsFailure)
        {
            return Result.Failure<WorkspaceAccessProfileDto>(permissions.Error);
        }

        string key = $"custom-{request.RequestId:N}";
        AccessProfileDto? existing = await profileReader.FindProfileByKeyAsync(
            scope.Value,
            key,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return Matches(existing, request, permissions.Value)
                ? Result.Success(ToProduct(existing))
                : Result.Failure<WorkspaceAccessProfileDto>(
                    WorkspaceAccessManagementErrors.RequestConflict);
        }

        Result<AccessProfileDto> created = await profiles.CreateProfileAsync(
            scope.Value,
            new AccessProfileDefinition(
                key,
                request.DisplayName,
                request.Description,
                permissions.Value),
            actor,
            cancellationToken).ConfigureAwait(false);
        return created.IsFailure
            ? Result.Failure<WorkspaceAccessProfileDto>(created.Error)
            : Result.Success(ToProduct(created.Value));
    }

    public async Task<Result<WorkspaceAccessProfileDto>> UpdateProfileAsync(
        Guid profileId,
        WorkspaceAccessProfileUpdate request,
        AccessSubject actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Result<AccessScope> scope = this.GetScope();
        if (scope.IsFailure)
        {
            return Result.Failure<WorkspaceAccessProfileDto>(scope.Error);
        }

        Result access = await this.AuthorizeManageAsync(
            actor,
            scope.Value,
            cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return Result.Failure<WorkspaceAccessProfileDto>(access.Error);
        }

        Result<IReadOnlyList<string>> permissions = await this.ValidatePermissionsAsync(
            request.Permissions,
            scope.Value,
            actor,
            cancellationToken).ConfigureAwait(false);
        if (permissions.IsFailure)
        {
            return Result.Failure<WorkspaceAccessProfileDto>(permissions.Error);
        }

        Result<AccessProfileDto> updated = await profiles.UpdateProfileAsync(
            profileId,
            scope.Value,
            new AccessProfileUpdate(
                request.DisplayName,
                request.Description,
                permissions.Value,
                request.ExpectedVersion),
            actor,
            cancellationToken).ConfigureAwait(false);
        return updated.IsFailure
            ? Result.Failure<WorkspaceAccessProfileDto>(updated.Error)
            : Result.Success(ToProduct(updated.Value));
    }

    public async Task<Result> ArchiveProfileAsync(
        Guid profileId,
        long expectedVersion,
        AccessSubject actor,
        CancellationToken cancellationToken = default)
    {
        Result<AccessScope> scope = this.GetScope();
        if (scope.IsFailure)
        {
            return Result.Failure(scope.Error);
        }

        Result access = await this.AuthorizeManageAsync(
            actor,
            scope.Value,
            cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return access;
        }

        Result<AccessProfileDto> profile = await profiles.GetProfileAsync(
            profileId,
            scope.Value,
            actor,
            cancellationToken).ConfigureAwait(false);
        if (profile.IsFailure)
        {
            return Result.Failure(profile.Error);
        }

        if (ProtectedSeedKeys.Contains(profile.Value.Key))
        {
            return Result.Failure(WorkspaceAccessManagementErrors.SeedProfileProtected);
        }

        if (profile.Value.AssignmentCount > 0)
        {
            return Result.Failure(WorkspaceAccessManagementErrors.ProfileAssigned);
        }

        return await profiles.ArchiveProfileAsync(
            profileId,
            scope.Value,
            expectedVersion,
            actor,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<IReadOnlyList<string>>> ValidatePermissionsAsync(
        IReadOnlyCollection<string>? requested,
        AccessScope scope,
        AccessSubject actor,
        CancellationToken cancellationToken)
    {
        if (requested is null)
        {
            return Result.Failure<IReadOnlyList<string>>(
                WorkspaceAccessManagementErrors.PermissionsInvalid);
        }

        string[] normalized = requested
            .Select(permission => permission?.Trim() ?? string.Empty)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (normalized.Any(permission => !Catalogue.ContainsKey(permission)))
        {
            return Result.Failure<IReadOnlyList<string>>(
                WorkspaceAccessManagementErrors.PermissionsInvalid);
        }

        HashSet<string> selected = normalized.ToHashSet(StringComparer.Ordinal);
        if (normalized.Any(permission => Catalogue[permission].RequiredPermissions
            .Any(required => !selected.Contains(required))))
        {
            return Result.Failure<IReadOnlyList<string>>(
                WorkspaceAccessManagementErrors.PermissionDependencyMissing);
        }

        Result<IReadOnlyList<string>> allowed = await profiles.ListAllowedPermissionsAsync(
            scope,
            actor,
            cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure)
        {
            return Result.Failure<IReadOnlyList<string>>(allowed.Error);
        }

        HashSet<string> available = allowed.Value.ToHashSet(StringComparer.Ordinal);
        return normalized.All(available.Contains)
            ? Result.Success<IReadOnlyList<string>>(normalized)
            : Result.Failure<IReadOnlyList<string>>(
                WorkspaceAccessManagementErrors.PermissionsInvalid);
    }

    private async Task<Result> AuthorizeManageAsync(
        AccessSubject actor,
        AccessScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        AccessDecision decision = await authorization.AuthorizeAsync(
            new AccessRequirement(
                actor,
                PermissionCode.Create(AccessControlProfilePermissionCodes.Manage),
                scope),
            cancellationToken).ConfigureAwait(false);
        return decision.IsAllowed
            ? Result.Success()
            : Result.Failure(AccessProfileManagementErrors.AccessDenied);
    }

    private Result<AccessScope> GetScope()
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<AccessScope>(WorkspaceAccessManagementErrors.ScopeRequired);
        }

        return Result.Success(WorkspaceAccessScopes.Create(scopeContext.ScopeId));
    }

    private static WorkspaceAccessProfileDto ToProduct(AccessProfileDto profile) => new(
        profile.Id,
        profile.Key,
        profile.DisplayName,
        profile.Description,
        profile.Status switch
        {
            AccessProfileStatus.Active => WorkspaceAccessProfileStatus.Active,
            AccessProfileStatus.Archived => WorkspaceAccessProfileStatus.Archived,
            _ => WorkspaceAccessProfileStatus.Unknown
        },
        profile.Version,
        profile.Permissions,
        profile.AssignmentCount,
        ProtectedSeedKeys.Contains(profile.Key),
        profile.CreatedAtUtc,
        profile.LastChangedAtUtc);

    private static bool Matches(
        AccessProfileDto existing,
        WorkspaceAccessProfileCreation request,
        IReadOnlyCollection<string> permissions) =>
        string.Equals(existing.DisplayName, request.DisplayName?.Trim(), StringComparison.Ordinal) &&
        string.Equals(existing.Description, request.Description?.Trim() ?? string.Empty, StringComparison.Ordinal) &&
        existing.Permissions.Order(StringComparer.Ordinal)
            .SequenceEqual(permissions.Order(StringComparer.Ordinal), StringComparer.Ordinal);
}
