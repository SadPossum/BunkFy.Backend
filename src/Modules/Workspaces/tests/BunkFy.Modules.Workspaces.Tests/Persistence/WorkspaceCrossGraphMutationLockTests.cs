namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Persistence;
using BunkFy.Modules.Workspaces.Persistence.Repositories;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceCrossGraphMutationLockTests
{
    [Fact]
    public async Task Valid_non_relational_tenant_coordinate_is_a_no_op()
    {
        await using WorkspacesDbContext dbContext =
            CreateDbContext("tenant-a");
        WorkspaceCrossGraphMutationLock mutationLock = new(dbContext);

        await mutationLock.AcquireAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData(" ", true)]
    [InlineData("tenant-a", false)]
    public async Task Invalid_tenant_coordinate_fails_closed(
        string tenantId,
        bool enabled)
    {
        await using WorkspacesDbContext dbContext =
            CreateDbContext(tenantId, enabled);
        WorkspaceCrossGraphMutationLock mutationLock = new(dbContext);

        await Assert.ThrowsAsync<
            WorkspaceOperationalMutationRejectedException>(() =>
            mutationLock.AcquireAsync(CancellationToken.None));
    }

    private static WorkspacesDbContext CreateDbContext(
        string tenantId,
        bool enabled = true) => new(
        new DbContextOptionsBuilder<WorkspacesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options,
        new TestScopeContext(tenantId, enabled));

    private sealed class TestScopeContext(
        string scopeId,
        bool enabled)
        : IScopeContext
    {
        public bool IsEnabled => enabled;
        public string ScopeId => scopeId;
    }
}
