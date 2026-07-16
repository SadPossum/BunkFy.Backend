namespace BunkFy.Modules.Reservations.Tests;

using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Persistence;
using BunkFy.Modules.Reservations.Persistence.Repositories;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationArrivalReminderTests
{
    [Fact]
    public async Task Expected_arrival_is_scheduled_two_hours_before_property_local_time()
    {
        await using ReservationsDbContext dbContext = CreateDbContext();
        ReservationArrivalReminderRepository repository = new(dbContext, new TestIdGenerator());
        Guid propertyId = Guid.NewGuid();
        Guid reservationId = Guid.NewGuid();

        await repository.ApplyPropertyAsync(
            new("tenant-a", propertyId, "Europe/Moscow", true, 1, new(2026, 7, 15, 10, 0, 0, TimeSpan.Zero)),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();
        await repository.RefreshReservationAsync(
            new(
                "tenant-a",
                reservationId,
                propertyId,
                new DateOnly(2026, 7, 16),
                new TimeOnly(15, 30),
                "Maya Chen",
                1),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        ReservationArrivalReminder reminder = Assert.Single(dbContext.ArrivalReminders);
        Assert.Equal(new DateTimeOffset(2026, 7, 16, 12, 30, 0, TimeSpan.Zero), reminder.ExpectedArrivalAtUtc);
        Assert.Equal(new DateTimeOffset(2026, 7, 16, 10, 30, 0, TimeSpan.Zero), reminder.DueAtUtc);
        Assert.Equal(ReservationArrivalReminderState.Pending, reminder.State);
    }

    [Fact]
    public async Task New_details_revision_supersedes_the_old_schedule()
    {
        await using ReservationsDbContext dbContext = CreateDbContext();
        ReservationArrivalReminderRepository repository = new(dbContext, new TestIdGenerator());
        Guid propertyId = Guid.NewGuid();
        Guid reservationId = Guid.NewGuid();
        await repository.ApplyPropertyAsync(
            new("tenant-a", propertyId, "UTC", true, 1, new(2026, 7, 15, 10, 0, 0, TimeSpan.Zero)),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        await repository.RefreshReservationAsync(
            new("tenant-a", reservationId, propertyId, new(2026, 7, 16), new(15, 30), "Maya Chen", 1),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();
        await repository.RefreshReservationAsync(
            new("tenant-a", reservationId, propertyId, new(2026, 7, 16), new(17, 0), "Maya Chen", 2),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        ReservationArrivalReminder[] reminders = await dbContext.ArrivalReminders
            .OrderBy(item => item.DetailsRevision)
            .ToArrayAsync();
        Assert.Equal(2, reminders.Length);
        Assert.Equal(ReservationArrivalReminderState.Superseded, reminders[0].State);
        Assert.Equal(ReservationArrivalReminderState.Pending, reminders[1].State);
        Assert.Equal(new DateTimeOffset(2026, 7, 16, 15, 0, 0, TimeSpan.Zero), reminders[1].DueAtUtc);
    }

    [Fact]
    public async Task Invalid_daylight_saving_local_time_does_not_create_a_reminder()
    {
        await using ReservationsDbContext dbContext = CreateDbContext();
        ReservationArrivalReminderRepository repository = new(dbContext, new TestIdGenerator());
        Guid propertyId = Guid.NewGuid();
        await repository.ApplyPropertyAsync(
            new("tenant-a", propertyId, "America/New_York", true, 1, new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero)),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        await repository.RefreshReservationAsync(
            new("tenant-a", Guid.NewGuid(), propertyId, new(2026, 3, 8), new(2, 30), "Guest", 1),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        Assert.Empty(dbContext.ArrivalReminders);
    }

    private static ReservationsDbContext CreateDbContext()
    {
        DbContextOptions<ReservationsDbContext> options = new DbContextOptionsBuilder<ReservationsDbContext>()
            .UseInMemoryDatabase($"reservation-reminders-{Guid.NewGuid():N}")
            .Options;
        return new(options, new TestScopeContext());
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
