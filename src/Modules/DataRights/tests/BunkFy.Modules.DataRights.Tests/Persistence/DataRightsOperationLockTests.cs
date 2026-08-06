namespace BunkFy.Modules.DataRights.Tests.Persistence;

using BunkFy.Modules.DataRights.Persistence;
using BunkFy.Modules.DataRights.Persistence.Repositories;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsOperationLockTests
{
    [Fact]
    public async Task Non_relational_locks_validate_and_complete()
    {
        await using DataRightsDbContext dbContext = CreateDbContext(
            new TestScopeContext("tenant-a", isEnabled: true));
        DataRightsOperationLock operationLock = new(dbContext);
        Guid coordinate = Guid.NewGuid();

        await operationLock.AcquireTenantControlAsync(CancellationToken.None);
        await operationLock.AcquireProcessReadAsync(
            coordinate,
            CancellationToken.None);
        await operationLock.AcquireProcessWriteAsync(
            coordinate,
            CancellationToken.None);
        await operationLock.AcquireOwnerWorkItemAsync(
            coordinate,
            CancellationToken.None);
        await operationLock.AcquireCaseReadAsync(
            coordinate,
            CancellationToken.None);
        await operationLock.AcquireCaseWriteAsync(
            coordinate,
            CancellationToken.None);
        await operationLock.AcquireExecutionWorkItemAsync(
            coordinate,
            CancellationToken.None);
        await operationLock.AcquireProcessingLedgerAsync(
            CancellationToken.None);
    }

    [Fact]
    public async Task Empty_identifiers_fail_closed()
    {
        await using DataRightsDbContext dbContext = CreateDbContext(
            new TestScopeContext("tenant-a", isEnabled: true));
        DataRightsOperationLock operationLock = new(dbContext);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            operationLock.AcquireProcessReadAsync(
                Guid.Empty,
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            operationLock.AcquireOwnerWorkItemAsync(
                Guid.Empty,
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            operationLock.AcquireCaseWriteAsync(
                Guid.Empty,
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            operationLock.AcquireExecutionWorkItemAsync(
                Guid.Empty,
                CancellationToken.None));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("tenant-a", false)]
    public async Task Missing_tenant_scope_fails_closed(
        string? scopeId,
        bool isEnabled)
    {
        await using DataRightsDbContext dbContext = CreateDbContext(
            new TestScopeContext(scopeId, isEnabled));
        DataRightsOperationLock operationLock = new(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            operationLock.AcquireTenantControlAsync(
                CancellationToken.None));
    }

    private static DataRightsDbContext CreateDbContext(
        IScopeContext scopeContext) => new(
        new DbContextOptionsBuilder<DataRightsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options,
        scopeContext);

    private sealed class TestScopeContext(
        string? scopeId,
        bool isEnabled) : IScopeContext
    {
        public bool IsEnabled => isEnabled;

        public string? ScopeId => scopeId;
    }
}
