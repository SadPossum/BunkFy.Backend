namespace BunkFy.Modules.Guests.Tests.Persistence;

using BunkFy.Modules.Guests.Application;
using BunkFy.Modules.Guests.Application.Commands;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Persistence;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GuestsPersistenceRetryBehaviorTests
{
    [Theory]
    [InlineData(2601, null)]
    [InlineData(2627, null)]
    [InlineData(null, "23505")]
    public void Unique_constraint_codes_are_provider_neutral(
        int? sqlServerError,
        string? postgreSqlState)
    {
        Assert.True(GuestsUniqueConstraintDetector.IsUniqueViolation(
            sqlServerError,
            postgreSqlState));
    }

    [Fact]
    public async Task Correction_persistence_conflict_reexecutes_once()
    {
        DbContextOptions<GuestsDbContext> options =
            new DbContextOptionsBuilder<GuestsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
        await using GuestsDbContext dbContext = new(options, new TestScopeContext());
        GuestsPersistenceRetryBehavior<
            ApplyGuestDataRightsCorrectionCommand,
            GuestDataRightsCorrectionReceiptDto> behavior = new(dbContext, _ => true);
        ApplyGuestDataRightsCorrectionCommand command = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            1,
            "Guest",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "user:operator");
        int attempts = 0;

        Task<Result<GuestDataRightsCorrectionReceiptDto>> Next()
        {
            attempts++;
            return attempts == 1
                ? throw new DbUpdateException("simulated unique conflict")
                : Task.FromResult(Result.Failure<GuestDataRightsCorrectionReceiptDto>(
                    GuestsApplicationErrors.CorrectionIdempotencyConflict));
        }

        Result<GuestDataRightsCorrectionReceiptDto> result =
            await behavior.HandleAsync(command, Next, CancellationToken.None);

        Assert.Equal(2, attempts);
        Assert.Equal(GuestsApplicationErrors.CorrectionIdempotencyConflict, result.Error);
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
