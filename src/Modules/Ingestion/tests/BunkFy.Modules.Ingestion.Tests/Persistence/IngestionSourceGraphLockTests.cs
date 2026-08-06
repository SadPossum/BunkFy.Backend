namespace BunkFy.Modules.Ingestion.Tests.Persistence;

using BunkFy.Modules.Ingestion.Persistence;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class IngestionSourceGraphLockTests
{
    [Fact]
    public async Task Non_relational_test_lock_creates_no_persistent_state()
    {
        await using IngestionDbContext dbContext = CreateInMemoryDbContext();
        IngestionSourceGraphLock sourceLock = new(dbContext);

        await sourceLock.AcquireAsync(
            "tenant-a",
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Invalid_or_cross_scope_coordinates_fail_closed()
    {
        await using IngestionDbContext dbContext = CreateInMemoryDbContext();
        IngestionSourceGraphLock sourceLock = new(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sourceLock.AcquireAsync(
                "tenant-b",
                Guid.NewGuid(),
                CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sourceLock.AcquireAsync(
                "tenant-a",
                Guid.Empty,
                CancellationToken.None));
    }

    [Fact]
    public async Task Relational_provider_requires_an_active_transaction()
    {
        await using IngestionDbContext dbContext = new(
            new DbContextOptionsBuilder<IngestionDbContext>()
                .UseNpgsql(
                    "Host=127.0.0.1;Port=1;Database=unused;" +
                    "Username=unused;Password=unused")
                .Options,
            new TestScope());
        IngestionSourceGraphLock sourceLock = new(dbContext);

        InvalidOperationException failure =
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sourceLock.AcquireAsync(
                    "tenant-a",
                    Guid.NewGuid(),
                    CancellationToken.None));

        Assert.Equal(
            "An Ingestion source operation lock requires an active database transaction.",
            failure.Message);
    }

    private static IngestionDbContext CreateInMemoryDbContext() => new(
        new DbContextOptionsBuilder<IngestionDbContext>()
            .UseInMemoryDatabase(
                $"ingestion-source-lock-{Guid.NewGuid():N}")
            .Options,
        new TestScope());

    private sealed class TestScope : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
