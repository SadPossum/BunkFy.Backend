namespace BunkFy.Modules.Reservations.Tests;

using Gma.Framework.Pagination;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Persistence;
using BunkFy.Modules.Reservations.Persistence.Repositories;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationRepositoryPaginationTests
{
    [Fact]
    public async Task List_filters_before_counting_and_paging_with_stable_ordering()
    {
        await using ReservationsDbContext dbContext = CreateDbContext();
        Guid propertyId = Guid.NewGuid();
        Reservation first = CreateReservation(propertyId, "Grace Hopper", "ada@example.test", new DateOnly(2026, 8, 1));
        Reservation second = CreateReservation(propertyId, "Ada Lovelace", null, new DateOnly(2026, 8, 3));
        Reservation excludedBySearch = CreateReservation(propertyId, "Linus Torvalds", null, new DateOnly(2026, 8, 2));
        Reservation excludedByProperty = CreateReservation(Guid.NewGuid(), "Ada Elsewhere", null, new DateOnly(2026, 8, 4));
        Assert.True(first.ConfirmAllocation(
            first.AllocationRequestId,
            Guid.NewGuid(),
            allocationVersion: 1,
            Guid.NewGuid(),
            first.CreatedAtUtc.AddMinutes(1)).IsSuccess);
        dbContext.Reservations.AddRange(first, second, excludedBySearch, excludedByProperty);
        await dbContext.SaveChangesAsync();
        ReservationRepository repository = new(dbContext);

        ReservationListResponse result = await repository.ListAsync(
            propertyId,
            [ReservationStatus.PendingAllocation, ReservationStatus.Confirmed],
            "ADA",
            ReservationListOrder.ArrivalAscending,
            PageRequest.Normalize(page: 2, pageSize: 1),
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(1, result.PageSize);
        Assert.Equal(second.Id, Assert.Single(result.Reservations).ReservationId);
    }

    private static Reservation CreateReservation(
        Guid propertyId,
        string guestName,
        string? email,
        DateOnly arrival)
    {
        DateTimeOffset now = new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);
        return Reservation.Create(
            Guid.NewGuid(),
            "tenant-a",
            propertyId,
            Guid.NewGuid(),
            arrival,
            arrival.AddDays(2),
            [Guid.NewGuid()],
            guestName,
            email,
            null,
            1,
            ReservationSource.Direct,
            null,
            null,
            null,
            Guid.NewGuid(),
            Guid.NewGuid(),
            ReservationDetailsChangeOrigin.Staff,
            null,
            null,
            null,
            Guid.NewGuid(),
            now).Value;
    }

    private static ReservationsDbContext CreateDbContext()
    {
        DbContextOptions<ReservationsDbContext> options = new DbContextOptionsBuilder<ReservationsDbContext>()
            .UseInMemoryDatabase($"reservation-pagination-{Guid.NewGuid():N}")
            .Options;
        return new(options, new TestScopeContext());
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
