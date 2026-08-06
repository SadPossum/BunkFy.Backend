namespace BunkFy.Modules.Retention.Tests.Persistence;

using BunkFy.Modules.Retention.Persistence;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class RetentionMutationLockTests
{
    [Fact]
    public async Task Non_relational_locks_create_no_persistent_state()
    {
        await using RetentionDbContext dbContext = CreateInMemoryDbContext();
        RetentionMutationLock mutationLock = new(dbContext);

        await mutationLock.AcquireTenantTargetReadAsync(
            "tenant-a",
            CancellationToken.None);
        await mutationLock.AcquireTenantTargetWriteAsync(
            "tenant-a",
            CancellationToken.None);
        await mutationLock.AcquirePropertyTargetReadAsync(
            "tenant-a",
            Guid.NewGuid(),
            CancellationToken.None);
        await mutationLock.AcquirePropertyTargetWriteAsync(
            "tenant-a",
            Guid.NewGuid(),
            CancellationToken.None);
        await mutationLock.AcquireExecutionAsync(
            "tenant-a",
            Guid.NewGuid(),
            CancellationToken.None);
        await mutationLock.AcquireScheduleAsync(
            "tenant-a",
            "guests",
            "guest-operational",
            Guid.NewGuid(),
            1,
            CancellationToken.None);

        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Invalid_or_cross_scope_coordinates_fail_closed()
    {
        await using RetentionDbContext dbContext = CreateInMemoryDbContext();
        RetentionMutationLock mutationLock = new(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            mutationLock.AcquireTenantTargetReadAsync(
                "tenant-b",
                CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            mutationLock.AcquirePropertyTargetWriteAsync(
                "tenant-a",
                Guid.Empty,
                CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            mutationLock.AcquireExecutionAsync(
                "tenant-a",
                Guid.Empty,
                CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            mutationLock.AcquireScheduleAsync(
                "tenant-a",
                "bad:key",
                "guest-operational",
                null,
                1,
                CancellationToken.None));
    }

    [Fact]
    public async Task Relational_provider_requires_an_active_transaction()
    {
        await using RetentionDbContext dbContext = new(
            new DbContextOptionsBuilder<RetentionDbContext>()
                .UseNpgsql(
                    "Host=127.0.0.1;Port=1;Database=unused;" +
                    "Username=unused;Password=unused")
                .Options,
            new TestScopeContext());
        RetentionMutationLock mutationLock = new(dbContext);

        InvalidOperationException failure =
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                mutationLock.AcquireExecutionAsync(
                    "tenant-a",
                    Guid.NewGuid(),
                    CancellationToken.None));

        Assert.Equal(
            "A Retention mutation lock requires an active database transaction.",
            failure.Message);
    }

    private static RetentionDbContext CreateInMemoryDbContext() => new(
        new DbContextOptionsBuilder<RetentionDbContext>()
            .UseInMemoryDatabase($"retention-mutation-lock-{Guid.NewGuid():N}")
            .Options,
        new TestScopeContext());

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
