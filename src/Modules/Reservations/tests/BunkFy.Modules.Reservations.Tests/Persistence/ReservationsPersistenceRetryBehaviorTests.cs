namespace BunkFy.Modules.Reservations.Tests.Persistence;

using BunkFy.Modules.Reservations.Application;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Persistence;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationsPersistenceRetryBehaviorTests
{
    [Fact]
    public async Task Correction_unique_conflict_reexecutes_once()
    {
        DbContextOptions<ReservationsDbContext> options =
            new DbContextOptionsBuilder<ReservationsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
        await using ReservationsDbContext dbContext =
            new(options, new TestScopeContext());
        ReservationsPersistenceRetryBehavior<
            ApplyReservationDataRightsCorrectionCommand,
            ReservationDataRightsCorrectionReceiptDto> behavior =
            new(dbContext, _ => true);
        ApplyReservationDataRightsCorrectionCommand command = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            1,
            0,
            "Guest",
            null,
            null,
            1,
            null,
            null,
            null,
            "user:operator");
        int attempts = 0;

        Task<Result<ReservationDataRightsCorrectionReceiptDto>> Next()
        {
            attempts++;
            return attempts == 1
                ? throw new DbUpdateException("simulated unique conflict")
                : Task.FromResult(Result.Failure<ReservationDataRightsCorrectionReceiptDto>(
                    ReservationsApplicationErrors.CorrectionIdempotencyConflict));
        }

        Result<ReservationDataRightsCorrectionReceiptDto> result =
            await behavior.HandleAsync(command, Next, CancellationToken.None);

        Assert.Equal(2, attempts);
        Assert.Equal(
            ReservationsApplicationErrors.CorrectionIdempotencyConflict,
            result.Error);
    }

    [Fact]
    public async Task Data_hold_unique_conflict_reexecutes_once()
    {
        DbContextOptions<ReservationsDbContext> options =
            new DbContextOptionsBuilder<ReservationsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
        await using ReservationsDbContext dbContext =
            new(options, new TestScopeContext());
        ReservationsPersistenceRetryBehavior<
            PlaceReservationDataHoldCommand,
            ReservationDataHoldReceiptDto> behavior =
            new(dbContext, _ => true);
        PlaceReservationDataHoldCommand command = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ExpectedReservationVersion: 2,
            ExpectedDetailsRevision: 1,
            ReservationDataHoldReasonCodes.Dispute,
            "user:privacy");
        int attempts = 0;

        Task<Result<ReservationDataHoldReceiptDto>> Next()
        {
            attempts++;
            return attempts == 1
                ? throw new DbUpdateException("simulated unique conflict")
                : Task.FromResult(
                    Result.Failure<ReservationDataHoldReceiptDto>(
                        ReservationsApplicationErrors
                            .DataHoldIdempotencyConflict));
        }

        Result<ReservationDataHoldReceiptDto> result =
            await behavior.HandleAsync(
                command,
                Next,
                CancellationToken.None);

        Assert.Equal(2, attempts);
        Assert.Equal(
            ReservationsApplicationErrors.DataHoldIdempotencyConflict,
            result.Error);
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
