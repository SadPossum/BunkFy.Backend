namespace Integration.Tests.Composition;

using BunkFy.Extensions.DataRights.AccessControl;
using BunkFy.Extensions.DataRights.Organizations;
using BunkFy.Extensions.DataRights.TaskRuntime;
using BunkFy.Extensions.Operations.Notifications;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Guests.Persistence;
using BunkFy.Modules.Ingestion.Persistence;
using BunkFy.Modules.Inventory.Persistence;
using BunkFy.Modules.Properties.Persistence;
using BunkFy.Modules.Reservations.Persistence;
using BunkFy.Modules.Retention.Persistence;
using BunkFy.Modules.Staff.Persistence;
using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Persistence;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Gma.Modules.AccessControl.Contracts;
using Gma.Modules.AccessControl.Persistence;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Persistence;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Persistence;
using Gma.Modules.TaskRuntime.Contracts;
using Gma.Modules.TaskRuntime.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

public sealed class TenantTerminationContributorCompositionTests
{
    [Fact]
    [Trait("Category", "Composition")]
    public void All_owner_contributors_are_distinguishable_in_one_host()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Persistence:Provider"] = "PostgreSql";
        builder.Configuration["ConnectionStrings:PostgreSql"] =
            "Host=localhost;Database=bunkfy-composition;Username=test;Password=test";

        builder.Services.AddWorkspacesApplication(
            builder.Configuration,
            "default");
        builder.AddWorkspacesPersistence();
        builder.AddPropertiesPersistence();
        builder.AddInventoryPersistence();
        builder.AddReservationsPersistence();
        builder.AddGuestsPersistence();
        builder.AddStaffPersistence();
        builder.AddIngestionPersistence();
        builder.AddRetentionPersistence();
        builder.Services.AddBunkFyAccessControlDataRights();
        builder.Services.AddBunkFyOrganizationsDataRights();
        builder.Services.AddBunkFyTaskRuntimeDataRights();
        builder.Services.AddBunkFyOperationsNotifications();

        Assert.Equal(
            ExpectedPhaseContributors,
            RegisteredImplementations<ITenantTerminationContributor>(
                builder.Services));
        Assert.Equal(
            ExpectedExportContributors,
            RegisteredImplementations<ITenantTerminationExportContributor>(
                builder.Services));
    }

    [Fact]
    [Trait("Category", "Composition")]
    public void Access_control_owner_resolves_against_the_contracts_lifecycle()
    {
        const string workspaceId =
            "0cb43695-1654-471b-8044-f09ae1d6540b";
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Persistence:Provider"] = "PostgreSql";
        builder.Configuration["ConnectionStrings:PostgreSql"] =
            "Host=localhost;Database=bunkfy-composition;Username=test;Password=test";
        builder.Services.AddSingleton<IScopeContext>(
            new TestScopeContext(workspaceId));
        builder.Services.AddSingleton<ISystemClock>(new TestSystemClock());
        builder.AddAccessControlPersistence();
        builder.Services.AddBunkFyAccessControlDataRights();

        using ServiceProvider provider = builder.Services.BuildServiceProvider(
            validateScopes: true);
        using IServiceScope scope = provider.CreateScope();

        IAccessControlScopeLifecycle lifecycle = scope.ServiceProvider
            .GetRequiredService<IAccessControlScopeLifecycle>();
        ITenantTerminationContributor phaseContributor = scope.ServiceProvider
            .GetServices<ITenantTerminationContributor>()
            .Single();
        ITenantTerminationExportContributor exportContributor = scope
            .ServiceProvider
            .GetServices<ITenantTerminationExportContributor>()
            .Single();

        Assert.Equal(
            "Gma.Modules.AccessControl.Persistence",
            lifecycle.GetType().Assembly.GetName().Name);
        Assert.Equal(
            "AccessControlTenantTerminationContributor",
            phaseContributor.GetType().Name);
        Assert.Equal(
            phaseContributor.GetType(),
            exportContributor.GetType());
    }

    [Fact]
    [Trait("Category", "Composition")]
    public void Organizations_owner_resolves_against_the_generic_scope_lifecycle()
    {
        const string organizationId =
            "0cb43695-1654-471b-8044-f09ae1d6540b";
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Persistence:Provider"] = "PostgreSql";
        builder.Configuration["ConnectionStrings:PostgreSql"] =
            "Host=localhost;Database=bunkfy-composition;Username=test;Password=test";
        builder.Services.AddSingleton<IScopeContext>(
            new TestScopeContext(organizationId));
        builder.Services.AddSingleton<ISystemClock>(new TestSystemClock());
        builder.AddOrganizationsPersistence();
        builder.Services.AddBunkFyOrganizationsDataRights();

        using ServiceProvider provider = builder.Services.BuildServiceProvider(
            validateScopes: true);
        using IServiceScope scope = provider.CreateScope();

        IOrganizationScopeLifecycle lifecycle = scope.ServiceProvider
            .GetRequiredService<IOrganizationScopeLifecycle>();
        ITenantTerminationContributor phaseContributor = scope.ServiceProvider
            .GetServices<ITenantTerminationContributor>()
            .Single();
        ITenantTerminationExportContributor exportContributor = scope
            .ServiceProvider
            .GetServices<ITenantTerminationExportContributor>()
            .Single();

        Assert.Equal(
            "Gma.Modules.Organizations.Persistence",
            lifecycle.GetType().Assembly.GetName().Name);
        Assert.Equal(
            "OrganizationsTenantTerminationContributor",
            phaseContributor.GetType().Name);
        Assert.Equal(
            phaseContributor.GetType(),
            exportContributor.GetType());
    }

    [Fact]
    [Trait("Category", "Composition")]
    public void Operations_notifications_owner_resolves_against_the_generic_scope_lifecycle()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Persistence:Provider"] = "PostgreSql";
        builder.Configuration["ConnectionStrings:PostgreSql"] =
            "Host=localhost;Database=bunkfy-composition;Username=test;Password=test";
        builder.Services.AddSingleton<IScopeContext>(
            new TestScopeContext("tenant-a"));
        builder.Services.AddSingleton<ISystemClock>(new TestSystemClock());
        builder.AddNotificationsPersistence();
        builder.Services.AddBunkFyOperationsNotifications();

        using ServiceProvider provider = builder.Services.BuildServiceProvider(
            validateScopes: true);
        using IServiceScope scope = provider.CreateScope();

        INotificationScopeLifecycle lifecycle = scope.ServiceProvider
            .GetRequiredService<INotificationScopeLifecycle>();
        ITenantTerminationContributor phaseContributor = scope.ServiceProvider
            .GetServices<ITenantTerminationContributor>()
            .Single();
        ITenantTerminationExportContributor exportContributor = scope
            .ServiceProvider
            .GetServices<ITenantTerminationExportContributor>()
            .Single();

        Assert.Equal(
            "Gma.Modules.Notifications.Persistence",
            lifecycle.GetType().Assembly.GetName().Name);
        Assert.Equal(
            "OperationsNotificationsTenantTerminationContributor",
            phaseContributor.GetType().Name);
        Assert.Equal(
            phaseContributor.GetType(),
            exportContributor.GetType());
    }

    [Fact]
    [Trait("Category", "Composition")]
    public void Task_runtime_owner_resolves_against_the_generic_scope_lifecycle()
    {
        const string workspaceId =
            "0cb43695-1654-471b-8044-f09ae1d6540b";
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Persistence:Provider"] = "PostgreSql";
        builder.Configuration["ConnectionStrings:PostgreSql"] =
            "Host=localhost;Database=bunkfy-composition;Username=test;Password=test";
        builder.Services.AddSingleton<IScopeContext>(
            new TestScopeContext(workspaceId));
        builder.Services.AddSingleton<ISystemClock>(new TestSystemClock());
        builder.AddTaskRuntimePersistence();
        builder.Services.AddBunkFyTaskRuntimeDataRights();

        using ServiceProvider provider = builder.Services.BuildServiceProvider(
            validateScopes: true);
        using IServiceScope scope = provider.CreateScope();

        ITaskRuntimeScopeLifecycle lifecycle = scope.ServiceProvider
            .GetRequiredService<ITaskRuntimeScopeLifecycle>();
        ITenantTerminationContributor phaseContributor = scope.ServiceProvider
            .GetServices<ITenantTerminationContributor>()
            .Single();
        ITenantTerminationExportContributor[] exportContributors = scope
            .ServiceProvider
            .GetServices<ITenantTerminationExportContributor>()
            .ToArray();

        Assert.Equal(
            "Gma.Modules.TaskRuntime.Persistence",
            lifecycle.GetType().Assembly.GetName().Name);
        Assert.Equal(
            "TaskRuntimeTenantTerminationContributor",
            phaseContributor.GetType().Name);
        Assert.Empty(exportContributors);
    }

    private static string[] RegisteredImplementations<TService>(
        IServiceCollection services) =>
        services
            .Where(descriptor => descriptor.ServiceType == typeof(TService))
            .Select(descriptor => descriptor.ImplementationType?.Name ??
                throw new InvalidOperationException(
                    $"{typeof(TService).Name} must use a typed registration."))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static readonly string[] ExpectedPhaseContributors =
    [
        "AccessControlTenantTerminationContributor",
        "GuestsTenantTerminationContributor",
        "IngestionTenantTerminationContributor",
        "InventoryTenantTerminationContributor",
        "OperationsNotificationsTenantTerminationContributor",
        "OrganizationsTenantTerminationContributor",
        "PropertiesTenantTerminationContributor",
        "ReservationsTenantTerminationContributor",
        "RetentionTenantTerminationContributor",
        "StaffTenantTerminationContributor",
        "TaskRuntimeTenantTerminationContributor",
        "WorkspaceTenantTerminationContributor"
    ];

    private static readonly string[] ExpectedExportContributors =
    [
        "AccessControlTenantTerminationContributor",
        "GuestsTenantTerminationContributor",
        "IngestionTenantTerminationContributor",
        "InventoryTenantTerminationContributor",
        "OperationsNotificationsTenantTerminationContributor",
        "OrganizationsTenantTerminationContributor",
        "PropertiesTenantTerminationContributor",
        "ReservationsTenantTerminationContributor",
        "RetentionTenantTerminationContributor",
        "StaffTenantTerminationContributor",
        "WorkspacesTenantTerminationExportContributor"
    ];

    private sealed class TestScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string? ScopeId => scopeId;
    }

    private sealed class TestSystemClock : ISystemClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
