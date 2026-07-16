namespace BunkFy.Modules.Ingestion.Tests.Persistence;

using BunkFy.Adapter.Abstractions;
using BunkFy.Modules.Ingestion.Application;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Persistence;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class IngestionPersistenceRetryBehaviorTests
{
    [Theory]
    [InlineData(2601, null)]
    [InlineData(2627, null)]
    [InlineData(null, "23505")]
    public void Unique_constraint_codes_are_provider_neutral(int? sqlServerError, string? postgreSqlState)
    {
        Assert.True(IngestionUniqueConstraintDetector.IsUniqueViolation(sqlServerError, postgreSqlState));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lease_claim_persistence_conflict_reexecutes_once(bool translatedConcurrency)
    {
        DbContextOptions<IngestionDbContext> options =
            new DbContextOptionsBuilder<IngestionDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
        await using IngestionDbContext dbContext = new(options, new TestScopeContext());
        IngestionPersistenceRetryBehavior<ClaimRemoteAdapterLeaseCommand, AdapterRemoteLeaseClaimResponse> behavior =
            new(dbContext, _ => true);
        ClaimRemoteAdapterLeaseCommand command = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new AdapterRemoteLeaseClaimRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "fake-http",
                ProtocolVersion: 1,
                ConfigurationSchemaVersion: 1,
                RequestedLeaseSeconds: 60));
        int attempts = 0;

        Task<Result<AdapterRemoteLeaseClaimResponse>> Next()
        {
            attempts++;
            if (attempts == 1)
            {
                if (translatedConcurrency)
                {
                    throw new OptimisticConcurrencyException(
                        "ingestion",
                        new DbUpdateConcurrencyException("simulated concurrency conflict"));
                }

                throw new DbUpdateException("simulated unique conflict");
            }

            return Task.FromResult(Result.Failure<AdapterRemoteLeaseClaimResponse>(
                IngestionApplicationErrors.RemoteLeaseUnavailable));
        }

        Result<AdapterRemoteLeaseClaimResponse> result = await behavior.HandleAsync(
            command, Next, CancellationToken.None);

        Assert.Equal(2, attempts);
        Assert.Equal(IngestionApplicationErrors.RemoteLeaseUnavailable, result.Error);
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string? ScopeId => "tenant-a";
    }
}
