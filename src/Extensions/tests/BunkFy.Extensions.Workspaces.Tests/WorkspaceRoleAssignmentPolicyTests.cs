namespace BunkFy.Extensions.Workspaces.Tests;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Observability;
using Gma.Framework.Runtime.Time;
using Gma.Modules.AccessControl.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceRoleAssignmentPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 28, 8, 0, 0, TimeSpan.Zero);
    private static readonly AccessScope WorkspaceScope = WorkspaceAccessScopes.Create("workspace-a");

    [Fact]
    public async Task Non_admin_actor_assignment_is_left_to_other_policies()
    {
        WorkspaceRoleAssignmentPolicy policy = CreatePolicy();

        bool allowed = await policy.IsAllowedAsync(
            CreateContext(AccessSubject.User("member-a"), WorkspaceScope, expiresAtUtc: null),
            CancellationToken.None);

        Assert.True(allowed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(481)]
    public async Task Admin_actor_requires_a_future_bounded_expiry(int? minutesFromNow)
    {
        WorkspaceRoleAssignmentPolicy policy = CreatePolicy();
        DateTimeOffset? expiresAtUtc = minutesFromNow is null
            ? null
            : Now.AddMinutes(minutesFromNow.Value);

        bool allowed = await policy.IsAllowedAsync(
            CreateContext(AccessSubject.AdminActor("support-a"), WorkspaceScope, expiresAtUtc),
            CancellationToken.None);

        Assert.False(allowed);
    }

    [Fact]
    public async Task Admin_actor_can_receive_a_bounded_workspace_lease()
    {
        WorkspaceRoleAssignmentPolicy policy = CreatePolicy();

        bool allowed = await policy.IsAllowedAsync(
            CreateContext(
                AccessSubject.AdminActor("support-a"),
                WorkspaceScope,
                Now.AddMinutes(480)),
            CancellationToken.None);

        Assert.True(allowed);
    }

    [Fact]
    public async Task Admin_actor_can_receive_a_bounded_property_lease()
    {
        WorkspaceRoleAssignmentPolicy policy = CreatePolicy();

        bool allowed = await policy.IsAllowedAsync(
            CreateContext(
                AccessSubject.AdminActor("support-a"),
                WorkspaceAccessScopes.CreateProperty("workspace-a", Guid.NewGuid()),
                Now.AddMinutes(30)),
            CancellationToken.None);

        Assert.True(allowed);
    }

    [Fact]
    public async Task Admin_actor_cannot_receive_an_arbitrary_role()
    {
        WorkspaceRoleAssignmentPolicy policy = CreatePolicy();

        bool allowed = await policy.IsAllowedAsync(
            CreateContext(
                AccessSubject.AdminActor("support-a"),
                WorkspaceScope,
                Now.AddMinutes(30),
                roleName: WorkspaceAccessRoles.LegacyMember),
            CancellationToken.None);

        Assert.False(allowed);
    }

    [Fact]
    public async Task Admin_actor_cannot_receive_a_role_with_permissions_above_the_support_ceiling()
    {
        WorkspaceRoleAssignmentPolicy policy = CreatePolicy();

        bool allowed = await policy.IsAllowedAsync(
            CreateContext(
                AccessSubject.AdminActor("support-a"),
                WorkspaceScope,
                Now.AddMinutes(30),
                permissions: [DataRightsAdminPermissionCodes.Export]),
            CancellationToken.None);

        Assert.False(allowed);
    }

    [Theory]
    [InlineData("global")]
    [InlineData("property:9e11c484-2999-44d5-b293-2c0962fdff76")]
    [InlineData("tenant:workspace-a/property:not-a-guid")]
    [InlineData("tenant:workspace-a/property:00000000-0000-0000-0000-000000000000")]
    [InlineData("tenant:workspace-a/property:9e11c484-2999-44d5-b293-2c0962fdff76/bed:1")]
    public async Task Admin_actor_cannot_receive_a_global_or_malformed_scope(string scopeValue)
    {
        WorkspaceRoleAssignmentPolicy policy = CreatePolicy();

        bool allowed = await policy.IsAllowedAsync(
            CreateContext(
                AccessSubject.AdminActor("support-a"),
                AccessScope.Parse(scopeValue),
                Now.AddMinutes(30)),
            CancellationToken.None);

        Assert.False(allowed);
    }

    [Fact]
    public void Registration_binds_and_validates_the_maximum_lease()
    {
        Dictionary<string, string?> values = new()
        {
            [$"{BunkFySupportAccessOptions.SectionName}:MaximumGrantMinutes"] = "1441"
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        ServiceCollection services = new();

        services.AddBunkFySupportAccess(configuration);

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IAccessRoleAssignmentPolicy) &&
            descriptor.ImplementationType == typeof(WorkspaceRoleAssignmentPolicy));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IAccessRoleAssignmentLifecycleObserver) &&
            descriptor.ImplementationType == typeof(SupportAccessLifecycleObserver));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ISecuritySignalDefinitionSource) &&
            descriptor.ImplementationType == typeof(SupportAccessSecuritySignalDefinitions));
        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(
            () => _ = provider.GetRequiredService<IOptions<BunkFySupportAccessOptions>>().Value);
    }

    private static WorkspaceRoleAssignmentPolicy CreatePolicy() =>
        new(
            Options.Create(new BunkFySupportAccessOptions { MaximumGrantMinutes = 480 }),
            new FixedClock(Now));

    private static AccessRoleAssignmentPolicyContext CreateContext(
        AccessSubject subject,
        AccessScope scope,
        DateTimeOffset? expiresAtUtc,
        string roleName = WorkspaceAccessRoles.CompanySupport,
        IReadOnlyCollection<string>? permissions = null) =>
        new(
            subject,
            roleName,
            scope,
            expiresAtUtc,
            permissions ?? [WorkspaceAccessRoles.CompanySupportPermissionCeiling[0]]);

    private sealed class FixedClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
