namespace BunkFy.Modules.Reservations.Tests.Persistence;

using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;
using BunkFy.Modules.Reservations.Persistence;
using BunkFy.Modules.Reservations.Persistence.Repositories;
using Gma.Framework.Pagination;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationProcessingRestrictionGateTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 25, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Ordinary_reads_deny_while_rights_and_continuation_reads_remain()
    {
        await using ReservationsDbContext dbContext = CreateDbContext();
        TestScopeContext scope = new();
        ReservationProcessingRestrictionProjectionRepository projections =
            new(dbContext, scope);
        ReservationRepository repository = new(dbContext, projections);
        Reservation reservation = CreateReservation();
        await repository.AddAsync(reservation, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        ReservationOperationLock operationLock =
            Assert.Single(dbContext.OperationLocks);
        Assert.Equal(reservation.ScopeId, operationLock.ScopeId);
        Assert.Equal(reservation.Id, operationLock.ReservationId);
        Assert.Equal(1, operationLock.Revision);

        Assert.NotNull(await repository.GetAsync(
            reservation.PropertyId,
            reservation.Id,
            CancellationToken.None));

        ReservationProcessingRestrictionProjection state =
            await projections.GetAsync(
                reservation.PropertyId,
                reservation.Id,
                CancellationToken.None) ??
            throw new InvalidOperationException("Restriction state was not initialized.");
        Assert.True(state.Apply(0, 1, Now).IsSuccess);
        await dbContext.SaveChangesAsync();

        Assert.Null(await repository.GetAsync(
            reservation.PropertyId,
            reservation.Id,
            CancellationToken.None));
        Assert.Empty((await repository.ListAsync(
            reservation.PropertyId,
            statuses: null,
            search: null,
            ReservationListOrder.CreatedDescending,
            PageRequest.Normalize(1, 20),
            CancellationToken.None)).Reservations);
        Assert.NotNull(await repository.GetForDataRightsAsync(
            reservation.PropertyId,
            reservation.Id,
            CancellationToken.None));
        Assert.NotNull(await repository.GetForRequiredContinuationAsync(
            reservation.PropertyId,
            reservation.Id,
            CancellationToken.None));
    }

    [Fact]
    public async Task Missing_or_future_state_fails_closed()
    {
        await using ReservationsDbContext dbContext = CreateDbContext();
        Reservation reservation = CreateReservation();
        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync();
        ReservationProcessingRestrictionProjectionRepository projections =
            new(dbContext, new TestScopeContext());
        ReservationRepository repository = new(dbContext, projections);

        Assert.Null(await repository.GetAsync(
            reservation.PropertyId,
            reservation.Id,
            CancellationToken.None));

        ReservationProcessingRestrictionProjection state =
            ReservationProcessingRestrictionProjection.Create(
                reservation.ScopeId,
                reservation.PropertyId,
                reservation.Id,
                contractVersion: 2,
                reservation.CreatedAtUtc).Value;
        dbContext.ProcessingRestrictionProjections.Add(state);
        await dbContext.SaveChangesAsync();

        Assert.Null(await repository.GetAsync(
            reservation.PropertyId,
            reservation.Id,
            CancellationToken.None));
    }

    [Fact]
    public async Task Existing_reservation_without_provisioned_operation_lock_fails_closed()
    {
        await using ReservationsDbContext dbContext = CreateDbContext();
        Reservation reservation = CreateReservation();
        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync();
        ReservationOperationLockRepository operationLock = new(dbContext);

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                operationLock.TryAcquireExistingAsync(
                    reservation.ScopeId,
                    reservation.Id,
                    CancellationToken.None));

        Assert.Equal(
            "The reservation operation lock is not provisioned.",
            exception.Message);
    }

    private static Reservation CreateReservation() => Reservation.Create(
        Guid.NewGuid(),
        "tenant-a",
        Guid.NewGuid(),
        Guid.NewGuid(),
        new DateOnly(2026, 8, 1),
        new DateOnly(2026, 8, 3),
        [Guid.NewGuid()],
        "Private Guest",
        "private@example.test",
        null,
        1,
        ReservationSource.Direct,
        null,
        null,
        null,
        Guid.NewGuid(),
        Guid.NewGuid(),
        ReservationDetailsChangeOrigin.Staff,
        "user:creator",
        null,
        null,
        Guid.NewGuid(),
        Now.AddDays(-1)).Value;

    private static ReservationsDbContext CreateDbContext()
    {
        DbContextOptions<ReservationsDbContext> options =
            new DbContextOptionsBuilder<ReservationsDbContext>()
                .UseInMemoryDatabase(
                    $"reservation-restriction-gate-{Guid.NewGuid():N}")
                .Options;
        return new(options, new TestScopeContext());
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
