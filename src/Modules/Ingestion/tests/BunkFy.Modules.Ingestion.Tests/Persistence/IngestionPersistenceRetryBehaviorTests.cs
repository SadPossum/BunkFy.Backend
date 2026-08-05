namespace BunkFy.Modules.Ingestion.Tests.Persistence;

using BunkFy.Adapter.Abstractions;
using BunkFy.Modules.Ingestion.Application;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Contracts;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Observation_persistence_conflict_reexecutes_once(bool translatedConcurrency)
    {
        DbContextOptions<IngestionDbContext> options =
            new DbContextOptionsBuilder<IngestionDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
        await using IngestionDbContext dbContext = new(options, new TestScopeContext());
        IngestionPersistenceRetryBehavior<ReceiveObservationCommand, AdapterObservationResult> behavior =
            new(dbContext, _ => true);
        ReceiveObservationCommand command = new(
            Guid.NewGuid(),
            RunId: null,
            Guid.NewGuid(),
            "reservation.v1",
            "external-1",
            "revision-1",
            SourceUpdatedAtUtc: null,
            DateTimeOffset.UtcNow,
            "application/json",
            ReadOnlyMemory<byte>.Empty,
            new string('a', 64));
        int attempts = 0;

        Task<Result<AdapterObservationResult>> Next()
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

            return Task.FromResult(Result.Success(new AdapterObservationResult(
                command.OperationId,
                AdapterObservationDisposition.Duplicate,
                Guid.NewGuid(),
                errorCode: null)));
        }

        Result<AdapterObservationResult> result = await behavior.HandleAsync(
            command,
            Next,
            CancellationToken.None);

        Assert.Equal(2, attempts);
        Assert.Equal(AdapterObservationDisposition.Duplicate, result.Value.Disposition);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Legal_hold_persistence_conflict_reexecutes_once(bool translatedConcurrency)
    {
        await AssertRetriesOnceAsync<PlaceLegalHoldCommand, LegalHoldDto>(
            new PlaceLegalHoldCommand(
                Guid.NewGuid(),
                "Regulatory request",
                "user:legal"),
            translatedConcurrency);
        await AssertRetriesOnceAsync<ReleaseLegalHoldCommand, LegalHoldDto>(
            new ReleaseLegalHoldCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                ExpectedVersion: 1,
                "Regulatory request closed",
                "user:legal"),
            translatedConcurrency);
    }

    private static async Task AssertRetriesOnceAsync<TCommand, TResponse>(
        TCommand command,
        bool translatedConcurrency)
        where TCommand : ICommand<TResponse>
    {
        DbContextOptions<IngestionDbContext> options =
            new DbContextOptionsBuilder<IngestionDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
        await using IngestionDbContext dbContext =
            new(options, new TestScopeContext());
        IngestionPersistenceRetryBehavior<TCommand, TResponse> behavior =
            new(dbContext, _ => true);
        int attempts = 0;

        Task<Result<TResponse>> Next()
        {
            attempts++;
            if (attempts == 1)
            {
                if (translatedConcurrency)
                {
                    throw new OptimisticConcurrencyException(
                        "ingestion",
                        new DbUpdateConcurrencyException(
                            "simulated concurrency conflict"));
                }

                throw new DbUpdateException("simulated unique conflict");
            }

            return Task.FromResult(Result.Failure<TResponse>(
                IngestionApplicationErrors.PropertyNotFound));
        }

        Result<TResponse> result = await behavior.HandleAsync(
            command,
            Next,
            CancellationToken.None);

        Assert.Equal(2, attempts);
        Assert.Equal(IngestionApplicationErrors.PropertyNotFound, result.Error);
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string? ScopeId => "tenant-a";
    }
}
