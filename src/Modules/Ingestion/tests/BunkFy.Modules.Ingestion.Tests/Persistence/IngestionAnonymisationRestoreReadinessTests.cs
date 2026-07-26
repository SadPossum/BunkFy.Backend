namespace BunkFy.Modules.Ingestion.Tests.Persistence;

using BunkFy.Modules.Ingestion.Domain.DataRights;
using BunkFy.Modules.Ingestion.Persistence;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

[Trait("Category", "Unit")]
public sealed class IngestionAnonymisationRestoreReadinessTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Reducing_tombstone_blocks_readiness_until_completed()
    {
        ServiceCollection services = new();
        services.AddSingleton<IScopeContext>(
            new TestScopeContext("tenant-a"));
        InMemoryDatabaseRoot databaseRoot = new();
        string databaseName =
            $"ingestion-restore-readiness-{Guid.NewGuid():N}";
        services.AddDbContext<IngestionDbContext>(options =>
            options.UseInMemoryDatabase(
                databaseName,
                databaseRoot));
        await using ServiceProvider provider = services.BuildServiceProvider();
        await using (AsyncServiceScope seedScope =
            provider.CreateAsyncScope())
        {
            IngestionDbContext dbContext = seedScope.ServiceProvider
                .GetRequiredService<IngestionDbContext>();
            dbContext.AnonymisationTombstones.Add(
                CreateTombstone("tenant-a"));
            await dbContext.SaveChangesAsync();
        }

        IngestionAnonymisationRestoreReadinessHealthCheck healthCheck =
            new(provider.GetRequiredService<IServiceScopeFactory>());
        HealthCheckResult reducing = await healthCheck.CheckHealthAsync(
            new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, reducing.Status);
        Assert.Equal(
            IngestionAnonymisationRestoreReadinessHealthCheck
                .IncompleteRestoreCode,
            reducing.Description);

        await using (AsyncServiceScope completeScope =
            provider.CreateAsyncScope())
        {
            IngestionDbContext dbContext = completeScope.ServiceProvider
                .GetRequiredService<IngestionDbContext>();
            IngestionAnonymisationTombstone tombstone = await dbContext
                .AnonymisationTombstones
                .IgnoreQueryFilters()
                .SingleAsync();
            Assert.True(tombstone.CompleteRestore(Now.AddMinutes(1)).IsSuccess);
            await dbContext.SaveChangesAsync();
        }

        HealthCheckResult completed = await healthCheck.CheckHealthAsync(
            new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, completed.Status);
    }

    [Fact]
    public async Task Live_execution_and_attached_replay_each_block_readiness()
    {
        ServiceCollection services = new();
        services.AddSingleton<IScopeContext>(
            new TestScopeContext("tenant-a"));
        InMemoryDatabaseRoot databaseRoot = new();
        string databaseName =
            $"ingestion-execution-readiness-{Guid.NewGuid():N}";
        services.AddDbContext<IngestionDbContext>(options =>
            options.UseInMemoryDatabase(
                databaseName,
                databaseRoot));
        await using ServiceProvider provider =
            services.BuildServiceProvider();
        Guid sourceLinkId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        Guid workItemId = Guid.NewGuid();
        Guid idempotencyKey = Guid.NewGuid();
        Guid caseId = Guid.NewGuid();
        Guid receiptId = Guid.NewGuid();
        await using (AsyncServiceScope seedScope =
            provider.CreateAsyncScope())
        {
            IngestionDbContext dbContext = seedScope.ServiceProvider
                .GetRequiredService<IngestionDbContext>();
            dbContext.AnonymisationTombstones.Add(
                IngestionAnonymisationTombstone.BeginExecution(
                    "tenant-a",
                    sourceLinkId,
                    propertyId,
                    Guid.NewGuid(),
                    selectedSourceLinkVersion: 1,
                    workItemId,
                    idempotencyKey,
                    caseId,
                    approvalRevision: 2,
                    operationRevision: 3,
                    receiptId,
                    new string('a', 64),
                    new string('b', 64),
                    new string('c', 64),
                    "user:executor",
                    graphRecordCount: 1,
                    fingerprintCount: 1,
                    rawPayloadCount: 0,
                    Now)
                .Value);
            await dbContext.SaveChangesAsync();
        }

        await using (AsyncServiceScope verifyScope =
            provider.CreateAsyncScope())
        {
            IngestionDbContext dbContext = verifyScope.ServiceProvider
                .GetRequiredService<IngestionDbContext>();
            IngestionAnonymisationTombstone persisted =
                await dbContext.AnonymisationTombstones
                    .IgnoreQueryFilters()
                    .SingleAsync();
            Assert.Equal(
                IngestionAnonymisationTombstoneState.Reducing,
                persisted.State);
            Assert.Equal(
                IngestionAnonymisationOrigin.LiveExecution,
                persisted.Origin);
        }

        IngestionAnonymisationRestoreReadinessHealthCheck healthCheck =
            new(provider.GetRequiredService<IServiceScopeFactory>());
        Assert.Equal(
            HealthStatus.Unhealthy,
            (await healthCheck.CheckHealthAsync(
                new HealthCheckContext())).Status);

        DateTimeOffset completedAtUtc = Now.AddMinutes(1);
        await using (AsyncServiceScope completeScope =
            provider.CreateAsyncScope())
        {
            IngestionDbContext dbContext = completeScope.ServiceProvider
                .GetRequiredService<IngestionDbContext>();
            IngestionAnonymisationTombstone tombstone =
                await dbContext.AnonymisationTombstones.SingleAsync();
            IngestionAnonymisationReceipt receipt =
                IngestionAnonymisationReceipt.Create(
                    receiptId,
                    "tenant-a",
                    workItemId,
                    idempotencyKey,
                    propertyId,
                    caseId,
                    approvalRevision: 2,
                    operationRevision: 3,
                    sourceLinkId,
                    selectedSourceLinkVersion: 1,
                    resultingSourceLinkVersion: 2,
                    graphRecordCount: 1,
                    fingerprintCount: 1,
                    rawPayloadCount: 0,
                    new string('a', 64),
                    new string('b', 64),
                    new string('c', 64),
                    "user:executor",
                    completedAtUtc)
                .Value;
            Assert.True(tombstone.CompleteExecution(receipt).IsSuccess);
            dbContext.AnonymisationReceipts.Add(receipt);
            await dbContext.SaveChangesAsync();
        }

        Assert.Equal(
            HealthStatus.Healthy,
            (await healthCheck.CheckHealthAsync(
                new HealthCheckContext())).Status);

        Guid ledgerEntryId = Guid.NewGuid();
        await using (AsyncServiceScope attachScope =
            provider.CreateAsyncScope())
        {
            IngestionDbContext dbContext = attachScope.ServiceProvider
                .GetRequiredService<IngestionDbContext>();
            IngestionAnonymisationTombstone tombstone =
                await dbContext.AnonymisationTombstones.SingleAsync();
            Assert.True(tombstone.BeginProtectedReplay(
                propertyId,
                ownerReceiptContractVersion: 1,
                receiptId,
                tombstone.OwnerReceiptSha256,
                resultingSourceLinkVersion: 2,
                completedAtUtc,
                ledgerEntryId,
                tenantSequence: 9,
                new string('d', 64),
                completedAtUtc.AddMinutes(1)).IsSuccess);
            await dbContext.SaveChangesAsync();
        }

        Assert.Equal(
            HealthStatus.Unhealthy,
            (await healthCheck.CheckHealthAsync(
                new HealthCheckContext())).Status);

        await using (AsyncServiceScope replayScope =
            provider.CreateAsyncScope())
        {
            IngestionDbContext dbContext = replayScope.ServiceProvider
                .GetRequiredService<IngestionDbContext>();
            IngestionAnonymisationTombstone tombstone =
                await dbContext.AnonymisationTombstones.SingleAsync();
            Assert.True(tombstone.CompleteRestore(
                completedAtUtc.AddMinutes(2)).IsSuccess);
            await dbContext.SaveChangesAsync();
        }

        Assert.Equal(
            HealthStatus.Healthy,
            (await healthCheck.CheckHealthAsync(
                new HealthCheckContext())).Status);
    }

    [Fact]
    public void Health_check_registration_is_idempotent()
    {
        HealthCheckServiceOptions options = new();
        IngestionAnonymisationRestoreHealthCheckOptionsSetup setup = new();

        setup.Configure(options);
        setup.Configure(options);

        Assert.Single(
            options.Registrations,
            registration => string.Equals(
                registration.Name,
                IngestionAnonymisationRestoreHealthCheckOptionsSetup
                    .RegistrationName,
                StringComparison.Ordinal));
    }

    private static IngestionAnonymisationTombstone CreateTombstone(
        string tenantId) =>
        IngestionAnonymisationTombstone.BeginRestore(
            tenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            selectedSourceLinkVersion: 1,
            resultingSourceLinkVersion: 2,
            ownerReceiptContractVersion: 1,
            Guid.NewGuid(),
            new string('a', IngestionAnonymisationTombstone.Sha256Length),
            Now.AddDays(-1),
            Guid.NewGuid(),
            tenantSequence: 1,
            new string('b', IngestionAnonymisationTombstone.Sha256Length),
            graphRecordCount: 1,
            fingerprintCount: 1,
            rawPayloadCount: 0,
            Now)
        .Value;

    private sealed class TestScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => scopeId;
    }
}
