namespace BunkFy.Modules.Retention.Tests.Persistence;

using BunkFy.Modules.Retention.Application.Ports;
using BunkFy.Modules.Retention.Contracts;
using BunkFy.Modules.Retention.Persistence;
using BunkFy.Modules.Retention.Persistence.Repositories;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

[Trait("Category", "Unit")]
public sealed class RetentionScopeRepositoryTests
{
    [Fact]
    public async Task Scheduler_enumerates_all_scopes_while_management_reads_remain_tenant_filtered()
    {
        InMemoryDatabaseRoot root = new();
        string databaseName = $"retention-scopes-{Guid.NewGuid():N}";
        Guid propertyA = Guid.NewGuid();
        Guid propertyB = Guid.NewGuid();

        await SeedTenantAsync(databaseName, root, "tenant-a", propertyA);
        await SeedTenantAsync(databaseName, root, "tenant-b", propertyB);

        await using RetentionDbContext context = CreateContext(
            databaseName,
            root,
            "tenant-a");
        RetentionScopeRepository repository = new(context);

        IReadOnlyList<RetentionScheduleTarget> schedulerTargets =
            await repository.ListActiveTargetsAsync(
                RetentionTargetScopeKind.Property,
                CancellationToken.None);
        IReadOnlyList<RetentionScheduleTarget> managementTargets =
            await repository.ListCurrentActiveTargetsAsync(
                RetentionTargetScopeKind.Property,
                CancellationToken.None);

        Assert.Equal(
            ["tenant-a", "tenant-b"],
            schedulerTargets.Select(target => target.ScopeId));
        RetentionScheduleTarget visible = Assert.Single(managementTargets);
        Assert.Equal("tenant-a", visible.ScopeId);
        Assert.Equal(propertyA, visible.PropertyId);
        Assert.True(await repository.IsActiveTargetAsync(
            RetentionTargetScopeKind.Property,
            propertyA,
            CancellationToken.None));
        Assert.False(await repository.IsActiveTargetAsync(
            RetentionTargetScopeKind.Property,
            propertyB,
            CancellationToken.None));
    }

    private static async Task SeedTenantAsync(
        string databaseName,
        InMemoryDatabaseRoot root,
        string scopeId,
        Guid propertyId)
    {
        await using RetentionDbContext context = CreateContext(
            databaseName,
            root,
            scopeId);
        RetentionScopeRepository repository = new(context);
        await repository.ApplyOrganizationAsync(
            new(scopeId, Guid.NewGuid(), true, 1),
            CancellationToken.None);
        await repository.ApplyPropertyTopologyAsync(
            new(scopeId, propertyId, true, 1),
            CancellationToken.None);
        await repository.ApplyPropertyPolicyAsync(
            new(scopeId, propertyId, true, 1, 1),
            CancellationToken.None);
        await context.SaveChangesAsync();
    }

    private static RetentionDbContext CreateContext(
        string databaseName,
        InMemoryDatabaseRoot root,
        string scopeId)
    {
        DbContextOptions<RetentionDbContext> options =
            new DbContextOptionsBuilder<RetentionDbContext>()
                .UseInMemoryDatabase(databaseName, root)
                .Options;
        return new(options, new TestScopeContext(scopeId));
    }

    private sealed class TestScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId { get; } = scopeId;
    }
}
