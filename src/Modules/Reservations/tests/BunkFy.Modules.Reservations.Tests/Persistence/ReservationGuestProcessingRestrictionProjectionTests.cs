namespace BunkFy.Modules.Reservations.Tests;

using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Reservations.Application.Handlers;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Persistence;
using BunkFy.Modules.Reservations.Persistence.Repositories;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationGuestProcessingRestrictionProjectionTests
{
    [Fact]
    public async Task Linkability_requires_current_unrestricted_owner_state_and_monotonic_updates()
    {
        TestScopeContext scope = new();
        await using ReservationsDbContext dbContext = CreateDbContext(scope);
        ReservationGuestProfileProjectionRepository repository = new(dbContext);
        Guid propertyId = Guid.NewGuid();
        Guid guestId = Guid.NewGuid();
        await repository.ApplyAsync(
            new(scope.ScopeId, guestId, propertyId, GuestStatus.Active, Version: 1),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        Assert.False(await repository.IsLinkableAsync(propertyId, guestId, CancellationToken.None));

        await repository.ApplyRestrictionAsync(
            new(
                scope.ScopeId,
                propertyId,
                guestId,
                GuestProcessingRestrictionContract.CurrentVersion + 1,
                Revision: 2,
                IsRestricted: false),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();
        Assert.False(await repository.IsLinkableAsync(propertyId, guestId, CancellationToken.None));

        await repository.ApplyRestrictionAsync(
            new(
                scope.ScopeId,
                propertyId,
                guestId,
                GuestProcessingRestrictionContract.CurrentVersion,
                Revision: 1,
                IsRestricted: false),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();
        Assert.False(await repository.IsLinkableAsync(propertyId, guestId, CancellationToken.None));

        await repository.ApplyRestrictionAsync(
            new(
                scope.ScopeId,
                propertyId,
                guestId,
                GuestProcessingRestrictionContract.CurrentVersion,
                Revision: 3,
                IsRestricted: false),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();
        Assert.True(await repository.IsLinkableAsync(propertyId, guestId, CancellationToken.None));

        await repository.ApplyRestrictionAsync(
            new(
                scope.ScopeId,
                propertyId,
                guestId,
                GuestProcessingRestrictionContract.CurrentVersion,
                Revision: 4,
                IsRestricted: true),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();
        Assert.False(await repository.IsLinkableAsync(propertyId, guestId, CancellationToken.None));

        await repository.ApplyRestrictionAsync(
            new(
                scope.ScopeId,
                propertyId,
                guestId,
                GuestProcessingRestrictionContract.CurrentVersion,
                Revision: 4,
                IsRestricted: false),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();
        Assert.False(await repository.IsLinkableAsync(propertyId, guestId, CancellationToken.None));

        await repository.ApplyRestrictionAsync(
            new(
                scope.ScopeId,
                propertyId,
                guestId,
                GuestProcessingRestrictionContract.CurrentVersion,
                Revision: 5,
                IsRestricted: false),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();
        Assert.True(await repository.IsLinkableAsync(propertyId, guestId, CancellationToken.None));
    }

    [Fact]
    public async Task Guest_events_initialize_and_advance_the_local_restriction_projection()
    {
        RecordingProjectionRepository repository = new();
        Guid propertyId = Guid.NewGuid();
        Guid guestId = Guid.NewGuid();
        DateTimeOffset nowUtc = new(2026, 7, 24, 18, 0, 0, TimeSpan.Zero);
        GuestProfileCreatedProjectionHandler createdHandler = new(repository);
        await createdHandler.HandleAsync(
            new GuestProfileCreatedIntegrationEvent(
                Guid.NewGuid(),
                "tenant-a",
                nowUtc,
                guestId,
                propertyId,
                GuestStatus.Active,
                guestVersion: 1),
            CancellationToken.None);

        ReservationGuestProcessingRestrictionProjectionWriteModel initial =
            Assert.Single(repository.Restrictions);
        Assert.Equal(GuestProcessingRestrictionContract.CurrentVersion, initial.ContractVersion);
        Assert.Equal(0, initial.Revision);
        Assert.False(initial.IsRestricted);

        GuestProcessingRestrictionChangedProjectionHandler changedHandler = new(repository);
        await changedHandler.HandleAsync(
            new GuestProcessingRestrictionChangedIntegrationEvent(
                Guid.NewGuid(),
                "tenant-a",
                nowUtc.AddMinutes(1),
                propertyId,
                guestId,
                GuestProcessingRestrictionContract.CurrentVersion,
                projectionRevision: 1,
                isRestricted: true),
            CancellationToken.None);

        ReservationGuestProcessingRestrictionProjectionWriteModel changed =
            Assert.Single(repository.Restrictions.Skip(1));
        Assert.Equal(propertyId, changed.PropertyId);
        Assert.Equal(guestId, changed.GuestId);
        Assert.Equal(1, changed.Revision);
        Assert.True(changed.IsRestricted);
    }

    private static ReservationsDbContext CreateDbContext(IScopeContext scope)
    {
        DbContextOptions<ReservationsDbContext> options =
            new DbContextOptionsBuilder<ReservationsDbContext>()
                .UseInMemoryDatabase($"reservation-guest-restrictions-{Guid.NewGuid():N}")
                .Options;
        return new(options, scope);
    }

    private sealed class RecordingProjectionRepository : IReservationGuestProfileProjectionRepository
    {
        public List<ReservationGuestProcessingRestrictionProjectionWriteModel> Restrictions { get; } = [];

        public Task<bool> IsLinkableAsync(
            Guid propertyId,
            Guid guestId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ApplyAsync(
            ReservationGuestProfileProjectionWriteModel profile,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ApplyRestrictionAsync(
            ReservationGuestProcessingRestrictionProjectionWriteModel restriction,
            CancellationToken cancellationToken)
        {
            this.Restrictions.Add(restriction);
            return Task.CompletedTask;
        }
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
