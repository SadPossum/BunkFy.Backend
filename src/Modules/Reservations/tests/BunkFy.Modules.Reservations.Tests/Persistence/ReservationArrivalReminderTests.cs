namespace BunkFy.Modules.Reservations.Tests;

using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;
using BunkFy.Modules.Reservations.Persistence;
using BunkFy.Modules.Reservations.Persistence.Repositories;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationArrivalReminderTests
{
    [Fact]
    public async Task Dispatch_won_after_candidate_scan_is_not_emitted_again()
    {
        await using ReservationsDbContext dbContext = CreateDbContext();
        Guid propertyId = Guid.NewGuid();
        Guid reservationId = Guid.NewGuid();
        Reservation reservation = CreateConfirmedReservation(
            propertyId,
            reservationId);
        dbContext.Reservations.Add(reservation);
        AddUnrestrictedState(dbContext, propertyId, reservationId);
        ReservationArrivalReminder? reminder = null;
        DateTimeOffset competingDispatchAtUtc =
            new(2026, 8, 10, 13, 29, 0, TimeSpan.Zero);
        CallbackOperationLock operationLock = new(
            async cancellationToken =>
            {
                Assert.NotNull(reminder);
                reminder.Dispatch(competingDispatchAtUtc);
                await dbContext.SaveChangesAsync(cancellationToken);
            });
        ReservationArrivalReminderRepository repository = new(
            dbContext,
            new TestIdGenerator(),
            operationLock);

        await repository.ApplyPropertyAsync(
            new(
                "tenant-a",
                propertyId,
                "UTC",
                true,
                1,
                new(2026, 8, 6, 10, 0, 0, TimeSpan.Zero)),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();
        await repository.RefreshReservationAsync(
            new(
                "tenant-a",
                reservationId,
                propertyId,
                reservation.Arrival,
                reservation.ExpectedArrivalTime,
                reservation.DetailsRevision),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();
        reminder = Assert.Single(dbContext.ArrivalReminders);

        ReservationArrivalReminderClaimResult claimed =
            await repository.ClaimDueAsync(
                new(2026, 8, 10, 13, 30, 0, TimeSpan.Zero),
                batchSize: 10,
                CancellationToken.None);

        Assert.Equal(1, claimed.ProcessedCount);
        Assert.Empty(claimed.Dispatches);
        Assert.Equal(1, operationLock.CallCount);
        Assert.Equal(ReservationArrivalReminderState.Dispatched, reminder.State);
        Assert.Equal(competingDispatchAtUtc, reminder.DispatchedAtUtc);
    }

    [Fact]
    public async Task Restriction_won_after_candidate_scan_suppresses_dispatch()
    {
        await using ReservationsDbContext dbContext = CreateDbContext();
        Guid propertyId = Guid.NewGuid();
        Guid reservationId = Guid.NewGuid();
        Reservation reservation = CreateConfirmedReservation(
            propertyId,
            reservationId);
        ReservationProcessingRestrictionProjection restriction =
            ReservationProcessingRestrictionProjection.Create(
                "tenant-a",
                propertyId,
                reservationId,
                ReservationProcessingRestrictionContract.CurrentVersion,
                new(2026, 8, 6, 10, 0, 0, TimeSpan.Zero)).Value;
        dbContext.Reservations.Add(reservation);
        dbContext.ProcessingRestrictionProjections.Add(restriction);
        CallbackOperationLock operationLock = new(
            async cancellationToken =>
            {
                Assert.True(restriction.Apply(
                    expectedRevision: 0,
                    ReservationProcessingRestrictionContract.CurrentVersion,
                    new(2026, 8, 10, 13, 29, 0, TimeSpan.Zero)).IsSuccess);
                await dbContext.SaveChangesAsync(cancellationToken);
            });
        ReservationArrivalReminderRepository repository = new(
            dbContext,
            new TestIdGenerator(),
            operationLock);

        await repository.ApplyPropertyAsync(
            new(
                "tenant-a",
                propertyId,
                "UTC",
                true,
                1,
                new(2026, 8, 6, 10, 0, 0, TimeSpan.Zero)),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();
        await repository.RefreshReservationAsync(
            new(
                "tenant-a",
                reservationId,
                propertyId,
                reservation.Arrival,
                reservation.ExpectedArrivalTime,
                reservation.DetailsRevision),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        ReservationArrivalReminderClaimResult claimed =
            await repository.ClaimDueAsync(
                new(2026, 8, 10, 13, 30, 0, TimeSpan.Zero),
                batchSize: 10,
                CancellationToken.None);

        Assert.Equal(1, claimed.ProcessedCount);
        Assert.Empty(claimed.Dispatches);
        Assert.Equal(1, operationLock.CallCount);
        Assert.Equal(
            ReservationArrivalReminderState.Superseded,
            Assert.Single(dbContext.ArrivalReminders).State);
    }

    [Fact]
    public async Task Expected_arrival_is_scheduled_two_hours_before_property_local_time()
    {
        await using ReservationsDbContext dbContext = CreateDbContext();
        ReservationArrivalReminderRepository repository = new(
            dbContext,
            new TestIdGenerator(),
            ReservationMutationTestSupport.AllowingOperationLock());
        Guid propertyId = Guid.NewGuid();
        Guid reservationId = Guid.NewGuid();
        AddUnrestrictedState(dbContext, propertyId, reservationId);

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
        ReservationArrivalReminderRepository repository = new(
            dbContext,
            new TestIdGenerator(),
            ReservationMutationTestSupport.AllowingOperationLock());
        Guid propertyId = Guid.NewGuid();
        Guid reservationId = Guid.NewGuid();
        AddUnrestrictedState(dbContext, propertyId, reservationId);
        await repository.ApplyPropertyAsync(
            new("tenant-a", propertyId, "UTC", true, 1, new(2026, 7, 15, 10, 0, 0, TimeSpan.Zero)),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        await repository.RefreshReservationAsync(
            new("tenant-a", reservationId, propertyId, new(2026, 7, 16), new(15, 30), 1),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();
        await repository.RefreshReservationAsync(
            new("tenant-a", reservationId, propertyId, new(2026, 7, 16), new(17, 0), 2),
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
        ReservationArrivalReminderRepository repository = new(
            dbContext,
            new TestIdGenerator(),
            ReservationMutationTestSupport.AllowingOperationLock());
        Guid propertyId = Guid.NewGuid();
        await repository.ApplyPropertyAsync(
            new("tenant-a", propertyId, "America/New_York", true, 1, new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero)),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        await repository.RefreshReservationAsync(
            new("tenant-a", Guid.NewGuid(), propertyId, new(2026, 3, 8), new(2, 30), 1),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        Assert.Empty(dbContext.ArrivalReminders);
    }

    [Fact]
    public async Task Property_policy_projection_is_independent_from_reminder_topology_ordering()
    {
        await using ReservationsDbContext dbContext = CreateDbContext();
        ReservationArrivalReminderRepository repository = new(
            dbContext,
            new TestIdGenerator(),
            ReservationMutationTestSupport.AllowingOperationLock());
        Guid propertyId = Guid.NewGuid();
        PropertyGovernancePolicyBinding binding = CreateGovernanceBinding();

        await repository.ApplyPropertyAsync(
            new("tenant-a", propertyId, "UTC", true, 5, new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero)),
            CancellationToken.None);
        await repository.ApplyPolicyAsync(
            new("tenant-a", propertyId, PropertyProcessingStatus.Enabled, binding, 3),
            CancellationToken.None);
        await repository.ApplyPropertyAsync(
            new("tenant-a", propertyId, "Europe/Moscow", false, 4, new(2026, 7, 22, 12, 1, 0, TimeSpan.Zero)),
            CancellationToken.None);
        await repository.ApplyPolicyAsync(
            new("tenant-a", propertyId, PropertyProcessingStatus.Suspended, binding, 2),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        ReservationPropertyPolicySnapshot snapshot = Assert.IsType<ReservationPropertyPolicySnapshot>(
            await repository.GetPolicyAsync(propertyId, CancellationToken.None));
        Assert.True(snapshot.IsActive);
        Assert.Equal(PropertyProcessingStatus.Enabled, snapshot.ProcessingStatus);
        ReservationPropertyProjection property = await dbContext.PropertyProjections.SingleAsync();
        Assert.Equal("UTC", property.TimeZoneId);
        Assert.Equal(5, property.TopologySourceVersion);
        Assert.Equal(3, property.PolicySourceVersion);
    }

    private static PropertyGovernancePolicyBinding CreateGovernanceBinding()
    {
        DateTimeOffset now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
        return new(
            "GB",
            "gb-hostel",
            1,
            "eu-west-2",
            "uk-no-transfer",
            "guest-operational",
            1,
            new string('a', PropertiesContractLimits.ContentSha256Length),
            now.AddDays(-1),
            now.AddDays(30),
            now,
            []);
    }

    private static Reservation CreateConfirmedReservation(
        Guid propertyId,
        Guid reservationId)
    {
        Reservation reservation = Reservation.Create(
            reservationId,
            "tenant-a",
            propertyId,
            Guid.NewGuid(),
            new DateOnly(2026, 8, 10),
            new DateOnly(2026, 8, 12),
            [Guid.NewGuid()],
            "Ada Guest",
            "ada@example.test",
            null,
            1,
            ReservationSource.Direct,
            sourceSystem: null,
            sourceReference: null,
            notes: null,
            Guid.NewGuid(),
            Guid.NewGuid(),
            ReservationDetailsChangeOrigin.Staff,
            initialDetailsActorId: "user:creator",
            initialAdapterConnectionId: null,
            initialExternalOperationId: null,
            Guid.NewGuid(),
            new(2026, 8, 6, 10, 0, 0, TimeSpan.Zero),
            new TimeOnly(15, 0),
            new TimeOnly(11, 0)).Value;
        Assert.True(reservation.ConfirmAllocation(
            reservation.AllocationRequestId,
            Guid.NewGuid(),
            allocationVersion: 1,
            Guid.NewGuid(),
            new(2026, 8, 6, 10, 1, 0, TimeSpan.Zero)).IsSuccess);
        reservation.ClearDomainEvents();
        return reservation;
    }

    private static void AddUnrestrictedState(
        ReservationsDbContext dbContext,
        Guid propertyId,
        Guid reservationId) =>
        dbContext.ProcessingRestrictionProjections.Add(
            ReservationProcessingRestrictionProjection.Create(
                "tenant-a",
                propertyId,
                reservationId,
                ReservationProcessingRestrictionContract.CurrentVersion,
                new(2026, 7, 15, 10, 0, 0, TimeSpan.Zero)).Value);

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

    private sealed class CallbackOperationLock(
        Func<CancellationToken, Task> acquired)
        : IReservationOperationLock
    {
        public int CallCount { get; private set; }

        public async Task<bool> TryAcquireExistingAsync(
            string tenantId,
            Guid reservationId,
            CancellationToken cancellationToken)
        {
            this.CallCount++;
            await acquired(cancellationToken);
            return true;
        }

        public Task AcquireCoordinateAsync(
            string tenantId,
            Guid reservationId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
