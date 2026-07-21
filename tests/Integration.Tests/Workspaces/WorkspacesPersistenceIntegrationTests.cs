namespace Integration.Tests;

using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using BunkFy.Modules.Workspaces.Persistence;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class WorkspacesPersistenceIntegrationTests
{
    private const string StaffAccessLifecycleMigration =
        "20260721174651_AddWorkspaceStaffAccessLifecycle";
    private const string ScopedStaffAccessSnapshotsMigration =
        "20260721203218_ScopeWorkspaceStaffAccessSnapshots";
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Staff_onboarding_migration_enforces_scope_uniqueness_and_concurrency()
    {
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("bunkfy_workspaces_tests")
            .Build();
        await postgreSql.StartAsync();

        Guid sourceId = Guid.NewGuid();
        string subjectId = Guid.NewGuid().ToString("D");
        WorkspaceStaffOnboarding tenantA = CreateApplication(TenantA, sourceId, subjectId);
        WorkspaceStaffOnboarding tenantB = CreateApplication(TenantB, sourceId, subjectId);

        await using (WorkspacesDbContext first = CreateDbContext(
            postgreSql.GetConnectionString(), TenantA))
        {
            await first.Database.MigrateAsync();
            first.StaffOnboardingApplications.Add(tenantA);
            await first.SaveChangesAsync();
        }

        await using (WorkspacesDbContext second = CreateDbContext(
            postgreSql.GetConnectionString(), TenantB))
        {
            second.StaffOnboardingApplications.Add(tenantB);
            await second.SaveChangesAsync();
            Assert.Single(await second.StaffOnboardingApplications.ToArrayAsync());
        }

        await using (WorkspacesDbContext scoped = CreateDbContext(
            postgreSql.GetConnectionString(), TenantA))
        {
            WorkspaceStaffOnboarding visible = await scoped.StaffOnboardingApplications.SingleAsync();
            Assert.Equal(tenantA.Id, visible.Id);

            scoped.StaffOnboardingApplications.Add(CreateApplication(TenantA, sourceId, subjectId));
            await Assert.ThrowsAsync<DbUpdateException>(() => scoped.SaveChangesAsync());
        }

        await using WorkspacesDbContext current = CreateDbContext(
            postgreSql.GetConnectionString(), TenantA);
        await using WorkspacesDbContext stale = CreateDbContext(
            postgreSql.GetConnectionString(), TenantA);
        WorkspaceStaffOnboarding currentApplication = await current.StaffOnboardingApplications.SingleAsync();
        WorkspaceStaffOnboarding staleApplication = await stale.StaffOnboardingApplications.SingleAsync();

        Assert.True(currentApplication.UpdateSubmission(
            "verified@example.test", "Current profile", null, null, null, null, null, null,
            DateTimeOffset.UtcNow).IsSuccess);
        await current.SaveChangesAsync();
        Assert.True(staleApplication.UpdateSubmission(
            "verified@example.test", "Stale profile", null, null, null, null, null, null,
            DateTimeOffset.UtcNow.AddSeconds(1)).IsSuccess);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Staff_access_snapshot_migration_backfills_workspace_scope_and_allows_scoped_duplicates()
    {
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("bunkfy_workspaces_access_migration_tests")
            .Build();
        await postgreSql.StartAsync();

        Guid processId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        Guid staffMemberId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        Guid profileId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        DateTimeOffset changedAtUtc = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);

        await using (WorkspacesDbContext previous = CreateDbContext(
            postgreSql.GetConnectionString(), TenantA))
        {
            await previous.Database.GetService<IMigrator>().MigrateAsync(StaffAccessLifecycleMigration);
            await previous.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO workspaces.staff_access_processes (
                    "Id", "StaffMemberId", "SubjectId", "TargetState", "TargetStaffVersion",
                    "EffectiveOn", "RequestedBy", "State", "Version", "CreatedAtUtc",
                    "LastChangedAtUtc", "CompletedAtUtc", "ScopeId")
                VALUES (
                    {processId}, {staffMemberId}, {"member-a"}, {2}, {2L},
                    {new DateOnly(2026, 7, 21)}, {"user:owner"}, {4}, {1L}, {changedAtUtc},
                    {changedAtUtc}, {changedAtUtc}, {TenantA});

                INSERT INTO workspaces.staff_access_profile_snapshots ("ProfileId", "ProcessId")
                VALUES ({profileId}, {processId});
                """);
        }

        await using (WorkspacesDbContext upgraded = CreateDbContext(
            postgreSql.GetConnectionString(), TenantA))
        {
            await upgraded.Database.MigrateAsync();
            WorkspaceStaffAccessProcess process = await upgraded.StaffAccessProcesses
                .Include(item => item.ProfileSnapshots)
                .SingleAsync(item => item.Id == processId);
            WorkspaceStaffAccessProfileSnapshot snapshot = Assert.Single(process.ProfileSnapshots);
            Assert.Equal(profileId, snapshot.ProfileId);
            Assert.Equal("tenant:tenant-a", snapshot.AssignmentScope);

            await upgraded.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO workspaces.staff_access_profile_snapshots (
                    "ProfileId", "ProcessId", "AssignmentScope")
                VALUES ({profileId}, {processId}, {"tenant:tenant-a/property:property-a"});
                """);
        }

        await using WorkspacesDbContext verified = CreateDbContext(
            postgreSql.GetConnectionString(), TenantA);
        WorkspaceStaffAccessProcess reloaded = await verified.StaffAccessProcesses
            .Include(item => item.ProfileSnapshots)
            .SingleAsync(item => item.Id == processId);
        Assert.Equal(
            ["tenant:tenant-a", "tenant:tenant-a/property:property-a"],
            reloaded.ProfileSnapshots
                .Select(snapshot => snapshot.AssignmentScope)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Staff_access_plan_migration_preserves_existing_data_and_adds_scoped_plan_storage()
    {
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("bunkfy_workspaces_plan_migration_tests")
            .Build();
        await postgreSql.StartAsync();

        Guid sourceId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        Guid profileId = Guid.NewGuid();
        string subjectId = Guid.NewGuid().ToString("D");
        WorkspaceStaffOnboarding existing = CreateApplication(TenantA, sourceId, subjectId);

        await using (WorkspacesDbContext previous = CreateDbContext(
            postgreSql.GetConnectionString(), TenantA))
        {
            await previous.Database.GetService<IMigrator>().MigrateAsync(
                ScopedStaffAccessSnapshotsMigration);
            previous.StaffOnboardingApplications.Add(existing);
            await previous.SaveChangesAsync();
        }

        WorkspaceStaffAccessPlan plan = WorkspaceStaffAccessPlan.Create(
            sourceId,
            TenantA,
            WorkspaceStaffOnboardingSource.EnrollmentLink,
            profileId,
            "front-desk",
            [propertyId],
            "owner-a",
            DateTimeOffset.UtcNow).Value;
        Assert.True(plan.Activate(DateTimeOffset.UtcNow.AddSeconds(1)).IsSuccess);

        await using (WorkspacesDbContext upgraded = CreateDbContext(
            postgreSql.GetConnectionString(), TenantA))
        {
            await upgraded.Database.MigrateAsync();
            Assert.Equal(
                existing.Id,
                (await upgraded.StaffOnboardingApplications.SingleAsync()).Id);

            upgraded.StaffAccessPlans.Add(plan);
            upgraded.PropertyProjections.Add(new WorkspacePropertyProjection(
                TenantA,
                propertyId,
                "Main House",
                PropertyStatus.Active,
                1));
            await upgraded.SaveChangesAsync();
        }

        await using WorkspacesDbContext verified = CreateDbContext(
            postgreSql.GetConnectionString(), TenantA);
        WorkspaceStaffAccessPlan reloaded = await verified.StaffAccessPlans
            .Include(item => item.Properties)
            .SingleAsync(item => item.Id == sourceId);
        WorkspacePropertyProjection property = await verified.PropertyProjections
            .SingleAsync(item => item.Id == propertyId);

        Assert.Equal(WorkspaceStaffAccessPlanState.Active, reloaded.Status);
        Assert.Equal(propertyId, Assert.Single(reloaded.Properties).PropertyId);
        Assert.Equal("Main House", property.Name);
        Assert.Equal(PropertyStatus.Active, property.Status);
    }

    private static WorkspacesDbContext CreateDbContext(string connectionString, string scopeId)
    {
        DbContextOptions<WorkspacesDbContext> options = new DbContextOptionsBuilder<WorkspacesDbContext>()
            .UseNpgsql(
                connectionString,
                postgreSql => postgreSql
                    .MigrationsAssembly(WorkspacesMigrations.PostgreSqlAssembly)
                    .MigrationsHistoryTable(WorkspacesMigrations.HistoryTable, WorkspacesMigrations.Schema))
            .Options;
        return new WorkspacesDbContext(options, new TestScopeContext(scopeId));
    }

    private static WorkspaceStaffOnboarding CreateApplication(
        string scopeId,
        Guid sourceId,
        string subjectId) => WorkspaceStaffOnboarding.Create(
            Guid.NewGuid(),
            scopeId,
            WorkspaceStaffOnboardingSource.EnrollmentLink,
            sourceId,
            subjectId,
            "verified@example.test",
            "Ada Operator",
            null,
            null,
            null,
            null,
            null,
            null,
            DateTimeOffset.UtcNow).Value;

    private sealed class TestScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId { get; } = scopeId;
    }
}
