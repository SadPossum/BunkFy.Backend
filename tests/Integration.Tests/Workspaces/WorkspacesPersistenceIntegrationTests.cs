namespace Integration.Tests;

using BunkFy.Modules.Workspaces.Domain;
using BunkFy.Modules.Workspaces.Persistence;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class WorkspacesPersistenceIntegrationTests
{
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
