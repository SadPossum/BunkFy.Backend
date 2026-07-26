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
