namespace BunkFy.Modules.Guests.Tests.Persistence;

using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Persistence;
using BunkFy.Modules.Guests.Persistence.Repositories;
using Gma.Framework.Pagination;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GuestStayHistoryRepositoryTests
{
    [Fact]
    public async Task History_is_property_scoped_deterministic_and_bounded()
    {
        TestScopeContext scope = new();
        DbContextOptions<GuestsDbContext> options =
            new DbContextOptionsBuilder<GuestsDbContext>()
                .UseInMemoryDatabase($"guest-stay-history-{Guid.NewGuid():N}")
                .Options;
        await using GuestsDbContext dbContext = new(options, scope);
        GuestProcessingRestrictionProjectionRepository restrictions = new(dbContext, scope);
        GuestStayHistoryRepository repository = new(
            dbContext,
            restrictions,
            new NoopGuestOperationLock());
        Guid propertyId = Guid.NewGuid();
        Guid guestId = Guid.NewGuid();
        Guid oldestReservationId = Guid.NewGuid();
        Guid middleReservationId = Guid.NewGuid();
        Guid newestReservationId = Guid.NewGuid();
        dbContext.StayHistory.AddRange(
            Entry(scope.ScopeId, guestId, oldestReservationId, propertyId, new DateOnly(2026, 1, 1)),
            Entry(scope.ScopeId, guestId, middleReservationId, propertyId, new DateOnly(2026, 2, 1)),
            Entry(scope.ScopeId, guestId, newestReservationId, propertyId, new DateOnly(2026, 3, 1)),
            Entry(scope.ScopeId, guestId, Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 4, 1)),
            Entry(scope.ScopeId, Guid.NewGuid(), Guid.NewGuid(), propertyId, new DateOnly(2026, 5, 1)));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        GuestStayHistoryListResponse first = await repository.ListAsync(
            propertyId,
            guestId,
            new PageRequest(1, 2),
            CancellationToken.None);
        GuestStayHistoryListResponse second = await repository.ListAsync(
            propertyId,
            guestId,
            new PageRequest(2, 2),
            CancellationToken.None);

        Assert.Collection(
            first.Stays,
            stay => Assert.Equal(newestReservationId, stay.ReservationId),
            stay => Assert.Equal(middleReservationId, stay.ReservationId));
        Assert.True(first.HasMore);
        Assert.Equal(2, first.PageSize);
        Assert.Equal(oldestReservationId, Assert.Single(second.Stays).ReservationId);
        Assert.False(second.HasMore);
    }

    private static GuestStayHistoryEntry Entry(
        string scopeId,
        Guid guestId,
        Guid reservationId,
        Guid propertyId,
        DateOnly arrival) => new(
        scopeId,
        guestId,
        reservationId,
        propertyId,
        GuestStayRole.Primary,
        arrival,
        arrival.AddDays(1),
        GuestStayStatus.Confirmed,
        null,
        null,
        null,
        isCurrentParticipant: false,
        reservationVersion: 1,
        projectionContractVersion: GuestsModuleMetadata.StayHistoryProjectionVersion);

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
