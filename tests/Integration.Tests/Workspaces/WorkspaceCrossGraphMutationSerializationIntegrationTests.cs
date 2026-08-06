namespace Integration.Tests;

using BunkFy.Modules.Workspaces.Domain.Termination;
using BunkFy.Modules.Workspaces.Persistence;
using BunkFy.Modules.Workspaces.Persistence.Repositories;
using Gma.Framework.Scoping;
using Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class WorkspaceCrossGraphMutationSerializationIntegrationTests
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Exclusive_tenant_mutation_drains_and_blocks_only_its_tenant()
    {
        await using PostgreSqlContainer postgreSql =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("bunkfy_workspace_cross_graph_lock_tests")
                .Build();
        await postgreSql.StartAsync().ConfigureAwait(false);
        string connectionString = postgreSql.GetConnectionString();
        await MigrateAsync(connectionString).ConfigureAwait(false);

        await using WorkspacesDbContext sourceHolder =
            CreateDbContext(connectionString, TenantA);
        await using IDbContextTransaction sourceTransaction =
            await sourceHolder.Database.BeginTransactionAsync()
                .ConfigureAwait(false);
        await new WorkspaceStaffOnboardingOperationLock(sourceHolder)
            .AcquireSourceReadAsync(
                Guid.NewGuid(),
                CancellationToken.None).ConfigureAwait(false);

        await using WorkspacesDbContext exclusiveDb =
            CreateDbContext(connectionString, TenantA);
        await using IDbContextTransaction exclusiveTransaction =
            await exclusiveDb.Database.BeginTransactionAsync()
                .ConfigureAwait(false);
        Task exclusive = new WorkspaceCrossGraphMutationLock(exclusiveDb)
            .AcquireAsync(CancellationToken.None);
        await AssertStillWaitingAsync(exclusive).ConfigureAwait(false);

        await using (WorkspacesDbContext otherTenant =
            CreateDbContext(connectionString, TenantB))
        await using (IDbContextTransaction otherTenantTransaction =
            await otherTenant.Database.BeginTransactionAsync()
                .ConfigureAwait(false))
        {
            await new WorkspaceCrossGraphMutationLock(otherTenant)
                .AcquireAsync(CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(2))
                .ConfigureAwait(false);
            await otherTenantTransaction.RollbackAsync().ConfigureAwait(false);
        }

        await sourceTransaction.CommitAsync().ConfigureAwait(false);
        await exclusive.WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);

        await new WorkspaceStaffAccessOperationLock(exclusiveDb)
            .AcquireStaffAsync(Guid.NewGuid(), CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(2))
            .ConfigureAwait(false);

        await using WorkspacesDbContext waitingSource =
            CreateDbContext(connectionString, TenantA);
        await using IDbContextTransaction waitingSourceTransaction =
            await waitingSource.Database.BeginTransactionAsync()
                .ConfigureAwait(false);
        Task sourceWaiter = new WorkspaceStaffOnboardingOperationLock(
                waitingSource)
            .AcquireSourceReadAsync(
                Guid.NewGuid(),
                CancellationToken.None);
        await AssertStillWaitingAsync(sourceWaiter).ConfigureAwait(false);

        await exclusiveTransaction.CommitAsync().ConfigureAwait(false);
        await sourceWaiter.WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);
        await waitingSourceTransaction.RollbackAsync().ConfigureAwait(false);

        await ProveFrozenTenantRejectedAsync(connectionString)
            .ConfigureAwait(false);
    }

    private static async Task AssertStillWaitingAsync(Task operation)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(250))
            .ConfigureAwait(false);
        Assert.False(operation.IsCompleted);
    }

    private static async Task MigrateAsync(string connectionString)
    {
        await using WorkspacesDbContext dbContext =
            CreateDbContext(connectionString, TenantA);
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }

    private static async Task ProveFrozenTenantRejectedAsync(
        string connectionString)
    {
        await using (WorkspacesDbContext seed =
            CreateDbContext(connectionString, TenantA))
        {
            WorkspaceTerminationFence fence =
                WorkspaceTerminationFence.Freeze(
                    Guid.NewGuid(),
                    TenantA,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    approvalRevision: 1,
                    Guid.NewGuid(),
                    new string('a', 64),
                    "termination-operator",
                    new DateTimeOffset(
                        2026,
                        8,
                        6,
                        18,
                        0,
                        0,
                        TimeSpan.Zero)).Value;
            seed.WorkspaceTerminationFences.Add(fence);
            await seed.SaveChangesAsync().ConfigureAwait(false);
        }

        await using WorkspacesDbContext rejected =
            CreateDbContext(connectionString, TenantA);
        await using IDbContextTransaction transaction =
            await rejected.Database.BeginTransactionAsync()
                .ConfigureAwait(false);

        await Assert.ThrowsAsync<
            WorkspaceOperationalMutationRejectedException>(() =>
            new WorkspaceCrossGraphMutationLock(rejected)
                .AcquireAsync(CancellationToken.None));
        await transaction.RollbackAsync().ConfigureAwait(false);
    }

    private static WorkspacesDbContext CreateDbContext(
        string connectionString,
        string tenantId) => new(
        new DbContextOptionsBuilder<WorkspacesDbContext>()
            .UseNpgsql(
                connectionString,
                options => options.MigrationsAssembly(
                    WorkspacesMigrations.PostgreSqlAssembly))
            .Options,
        new TestScopeContext(tenantId));

    private sealed class TestScopeContext(string scopeId)
        : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => scopeId;
    }
}
