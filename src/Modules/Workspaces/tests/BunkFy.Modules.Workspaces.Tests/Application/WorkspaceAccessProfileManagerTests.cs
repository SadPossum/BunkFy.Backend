namespace BunkFy.Modules.Workspaces.Tests.Application;

using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Gma.Modules.AccessControl.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceAccessProfileManagerTests
{
    private static readonly Guid WorkspaceId = Guid.Parse("19f00525-c2eb-4388-acf2-379d59062a53");
    private static readonly AccessSubject Actor = AccessSubject.User("manager-a");

    [Fact]
    public void Product_catalogue_covers_every_delegable_permission_once()
    {
        string[] catalogue = WorkspaceAccessPermissionCatalogue.All
            .Select(permission => permission.Code)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] delegable = WorkspaceAccessRoles.DelegablePermissions
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(catalogue.Length, catalogue.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(delegable, catalogue);
        Assert.All(WorkspaceAccessPermissionCatalogue.All, permission =>
        {
            Assert.False(string.IsNullOrWhiteSpace(permission.Group));
            Assert.False(string.IsNullOrWhiteSpace(permission.Label));
            Assert.All(permission.RequiredPermissions, required =>
                Assert.Contains(required, catalogue, StringComparer.Ordinal));
        });
    }

    [Fact]
    public async Task Catalogue_intersects_product_permissions_with_current_gma_registry()
    {
        FakeAccessProfileManager profiles = new()
        {
            AllowedPermissions = WorkspaceAccessRoles.DelegablePermissions
                .Where(permission => !string.Equals(
                    permission,
                    InventoryAdminPermissionCodes.Read,
                    StringComparison.Ordinal))
                .ToArray()
        };
        WorkspaceAccessProfileManager manager = CreateManager(profiles);

        Result<WorkspaceAccessCatalogueDto> result = await manager.GetCatalogueAsync(Actor);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Value.Permissions, item =>
            string.Equals(item.Code, InventoryAdminPermissionCodes.Read, StringComparison.Ordinal));
        Assert.DoesNotContain(result.Value.Permissions, item =>
            item.RequiredPermissions.Contains(InventoryAdminPermissionCodes.Read, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Matching_create_replay_is_idempotent_and_still_requires_manage_access()
    {
        Guid requestId = Guid.NewGuid();
        FakeAccessProfileManager profiles = new();
        FakeAccessProfileProvisioner reader = new()
        {
            ExistingProfile = Profile(
                key: $"custom-{requestId:N}",
                displayName: "Night team",
                description: "Overnight operations",
                permissions:
                [
                    BunkFy.Modules.Properties.Contracts.PropertiesAdminPermissionCodes.Read,
                    InventoryAdminPermissionCodes.Read
                ])
        };
        RecordingAuthorization authorization = new();
        WorkspaceAccessProfileManager manager = CreateManager(profiles, reader, authorization);

        Result<WorkspaceAccessProfileDto> result = await manager.CreateProfileAsync(
            new WorkspaceAccessProfileCreation(
                requestId,
                " Night team ",
                " Overnight operations ",
                [
                    BunkFy.Modules.Properties.Contracts.PropertiesAdminPermissionCodes.Read,
                    InventoryAdminPermissionCodes.Read
                ]),
            Actor);

        Assert.True(result.IsSuccess);
        Assert.Empty(profiles.CreatedDefinitions);
        AccessRequirement requirement = Assert.Single(authorization.Requirements);
        Assert.Equal(AccessControlProfilePermissionCodes.Manage, requirement.Permission.Value);
    }

    [Fact]
    public async Task Changed_create_replay_returns_request_conflict_without_mutation()
    {
        Guid requestId = Guid.NewGuid();
        FakeAccessProfileManager profiles = new();
        FakeAccessProfileProvisioner reader = new()
        {
            ExistingProfile = Profile(key: $"custom-{requestId:N}", displayName: "Front desk")
        };
        WorkspaceAccessProfileManager manager = CreateManager(profiles, reader);

        Result<WorkspaceAccessProfileDto> result = await manager.CreateProfileAsync(
            new WorkspaceAccessProfileCreation(requestId, "Night team", null, []),
            Actor);

        Assert.True(result.IsFailure);
        Assert.Equal(WorkspaceAccessManagementErrors.RequestConflict, result.Error);
        Assert.Empty(profiles.CreatedDefinitions);
    }

    [Theory]
    [InlineData("unknown.permission", false)]
    [InlineData("inventory.blocks.manage", true)]
    public async Task Invalid_or_incomplete_permission_sets_fail_before_create(
        string permission,
        bool dependencyFailure)
    {
        FakeAccessProfileManager profiles = new();
        WorkspaceAccessProfileManager manager = CreateManager(profiles);

        Result<WorkspaceAccessProfileDto> result = await manager.CreateProfileAsync(
            new WorkspaceAccessProfileCreation(Guid.NewGuid(), "Invalid", null, [permission]),
            Actor);

        Assert.True(result.IsFailure);
        Assert.Equal(
            dependencyFailure
                ? WorkspaceAccessManagementErrors.PermissionDependencyMissing
                : WorkspaceAccessManagementErrors.PermissionsInvalid,
            result.Error);
        Assert.Empty(profiles.CreatedDefinitions);
    }

    [Fact]
    public async Task Update_forwards_normalized_permissions_and_expected_version()
    {
        FakeAccessProfileManager profiles = new();
        WorkspaceAccessProfileManager manager = CreateManager(profiles);
        Guid profileId = Guid.NewGuid();

        Result<WorkspaceAccessProfileDto> result = await manager.UpdateProfileAsync(
            profileId,
            new WorkspaceAccessProfileUpdate(
                "Front desk",
                null,
                [InventoryAdminPermissionCodes.BlocksManage, InventoryAdminPermissionCodes.Read,
                    BunkFy.Modules.Properties.Contracts.PropertiesAdminPermissionCodes.Read],
                7),
            Actor);

        Assert.True(result.IsSuccess);
        (Guid RecordedProfileId, AccessProfileUpdate Update) recorded = Assert.Single(profiles.Updates);
        Assert.Equal(profileId, recorded.RecordedProfileId);
        Assert.Equal(7, recorded.Update.ExpectedVersion);
        Assert.Equal(
            recorded.Update.Permissions.Order(StringComparer.Ordinal),
            recorded.Update.Permissions);
    }

    [Theory]
    [InlineData("manager", 0, "Workspaces.AccessProfileSeedProtected")]
    [InlineData("custom-assigned", 3, "Workspaces.AccessProfileAssigned")]
    public async Task Archive_protects_seed_and_assigned_profiles(
        string key,
        int assignmentCount,
        string expectedErrorCode)
    {
        FakeAccessProfileManager profiles = new()
        {
            Profile = Profile(key: key, assignmentCount: assignmentCount)
        };
        WorkspaceAccessProfileManager manager = CreateManager(profiles);

        Result result = await manager.ArchiveProfileAsync(profiles.Profile.Id, 4, Actor);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedErrorCode, result.Error.Code);
        Assert.Empty(profiles.Archives);
    }

    [Fact]
    public async Task Archive_denies_before_reading_profile_and_delegates_unassigned_custom_profile()
    {
        FakeAccessProfileManager deniedProfiles = new();
        WorkspaceAccessProfileManager denied = CreateManager(
            deniedProfiles,
            authorization: new RecordingAuthorization(allowed: false));

        Result deniedResult = await denied.ArchiveProfileAsync(Guid.NewGuid(), 2, Actor);

        Assert.True(deniedResult.IsFailure);
        Assert.Equal(AccessProfileManagementErrors.AccessDenied, deniedResult.Error);
        Assert.Equal(0, deniedProfiles.GetProfileCalls);

        FakeAccessProfileManager allowedProfiles = new()
        {
            Profile = Profile(key: "custom-safe")
        };
        WorkspaceAccessProfileManager allowed = CreateManager(allowedProfiles);

        Result allowedResult = await allowed.ArchiveProfileAsync(
            allowedProfiles.Profile.Id,
            6,
            Actor);

        Assert.True(allowedResult.IsSuccess);
        Assert.Equal((allowedProfiles.Profile.Id, 6L), Assert.Single(allowedProfiles.Archives));
    }

    private static WorkspaceAccessProfileManager CreateManager(
        FakeAccessProfileManager? profiles = null,
        FakeAccessProfileProvisioner? reader = null,
        RecordingAuthorization? authorization = null,
        IScopeContext? scope = null) => new(
            profiles ?? new FakeAccessProfileManager(),
            reader ?? new FakeAccessProfileProvisioner(),
            authorization ?? new RecordingAuthorization(),
            scope ?? new StubScopeContext(WorkspaceId.ToString("D")));

    private static AccessProfileDto Profile(
        string key,
        string displayName = "Profile",
        string description = "",
        IReadOnlyList<string>? permissions = null,
        int assignmentCount = 0) => new(
            Guid.NewGuid(),
            WorkspaceId.ToString("D"),
            key,
            displayName.Trim(),
            description.Trim(),
            AccessProfileStatus.Active,
            4,
            permissions ?? [],
            assignmentCount,
            new DateTimeOffset(2026, 7, 22, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 22, 9, 0, 0, TimeSpan.Zero));

    private sealed class FakeAccessProfileManager : IAccessProfileManager
    {
        public IReadOnlyList<string> AllowedPermissions { get; init; } =
            WorkspaceAccessRoles.DelegablePermissions;

        public AccessProfileDto Profile { get; init; } = WorkspaceAccessProfileManagerTests.Profile(
            "custom-profile");

        public List<AccessProfileDefinition> CreatedDefinitions { get; } = [];
        public List<(Guid ProfileId, AccessProfileUpdate Update)> Updates { get; } = [];
        public List<(Guid ProfileId, long ExpectedVersion)> Archives { get; } = [];
        public int GetProfileCalls { get; private set; }

        public Task<Result<AccessControlPage<AccessProfileDto>>> ListProfilesAsync(
            AccessScope ownerScope,
            bool includeArchived,
            int page,
            int pageSize,
            AccessSubject actor,
            CancellationToken cancellationToken = default) => Task.FromResult(Result.Success(
                new AccessControlPage<AccessProfileDto>([this.Profile], page, pageSize, false)));

        public Task<Result<IReadOnlyList<string>>> ListAllowedPermissionsAsync(
            AccessScope ownerScope,
            AccessSubject actor,
            CancellationToken cancellationToken = default) => Task.FromResult(
                Result.Success(this.AllowedPermissions));

        public Task<Result<AccessProfileDto>> GetProfileAsync(
            Guid profileId,
            AccessScope ownerScope,
            AccessSubject actor,
            CancellationToken cancellationToken = default)
        {
            this.GetProfileCalls++;
            return Task.FromResult(Result.Success(this.Profile));
        }

        public Task<Result<AccessProfileDto>> CreateProfileAsync(
            AccessScope ownerScope,
            AccessProfileDefinition definition,
            AccessSubject actor,
            CancellationToken cancellationToken = default)
        {
            this.CreatedDefinitions.Add(definition);
            return Task.FromResult(Result.Success(WorkspaceAccessProfileManagerTests.Profile(
                definition.Key,
                definition.DisplayName,
                definition.Description ?? string.Empty,
                definition.Permissions.ToArray())));
        }

        public Task<Result<AccessProfileDto>> UpdateProfileAsync(
            Guid profileId,
            AccessScope ownerScope,
            AccessProfileUpdate update,
            AccessSubject actor,
            CancellationToken cancellationToken = default)
        {
            this.Updates.Add((profileId, update));
            return Task.FromResult(Result.Success(WorkspaceAccessProfileManagerTests.Profile(
                this.Profile.Key,
                update.DisplayName,
                update.Description ?? string.Empty,
                update.Permissions.ToArray())));
        }

        public Task<Result> ArchiveProfileAsync(
            Guid profileId,
            AccessScope ownerScope,
            long expectedVersion,
            AccessSubject actor,
            CancellationToken cancellationToken = default)
        {
            this.Archives.Add((profileId, expectedVersion));
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class FakeAccessProfileProvisioner : IAccessProfileProvisioner
    {
        public AccessProfileDto? ExistingProfile { get; init; }

        public Task<AccessProfileDto?> FindProfileByKeyAsync(
            AccessScope ownerScope,
            string key,
            CancellationToken cancellationToken = default) => Task.FromResult(this.ExistingProfile);

        public Task<AccessProfileDto> EnsureProfileAsync(
            AccessScope ownerScope,
            AccessProfileDefinition definition,
            AccessSubject actor,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<AccessProfileAssignmentSet> GetSubjectAssignmentsAsync(
            AccessSubject subject,
            AccessScope ownerScope,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<AccessProfileAssignmentReconciliation> ReconcileSubjectAssignmentsAsync(
            AccessSubject subject,
            AccessScope ownerScope,
            IReadOnlyCollection<Guid> profileIds,
            AccessSubject actor,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingAuthorization(bool allowed = true) : IAccessAuthorizationService
    {
        public List<AccessRequirement> Requirements { get; } = [];

        public Task<AccessDecision> AuthorizeAsync(
            AccessRequirement requirement,
            CancellationToken cancellationToken)
        {
            this.Requirements.Add(requirement);
            return Task.FromResult(allowed
                ? AccessDecision.Allowed()
                : AccessDecision.Denied("test.denied"));
        }
    }

    private sealed class StubScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string? ScopeId => scopeId;
    }
}
