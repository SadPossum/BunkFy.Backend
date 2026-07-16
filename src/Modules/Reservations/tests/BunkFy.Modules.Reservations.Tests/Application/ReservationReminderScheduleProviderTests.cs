namespace BunkFy.Modules.Reservations.Tests;

using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Application.Tasks;
using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Tasks;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationReminderScheduleProviderTests
{
    [Fact]
    public async Task Provider_creates_scoped_refresh_and_minute_dispatch_schedules()
    {
        ReservationReminderScheduleProvider provider = new(new FakeRepository(["tenant-b"]));

        ScheduledTaskDefinition[] schedules = (await provider.GetSchedulesAsync(CancellationToken.None))
            .ToArray();

        Assert.Equal(2, schedules.Length);
        Assert.All(schedules, schedule =>
        {
            Assert.Equal("tenant-b", schedule.ScopeId);
            Assert.True(schedule.RunOnStart);
            Assert.Equal(3, schedule.MaxAttempts);
        });
        ScheduledTaskDefinition refresh = Assert.Single(
            schedules,
            schedule => schedule.TaskName == RebuildReservationPropertiesPayload.TaskName);
        Assert.Equal(TimeSpan.FromHours(24), refresh.Interval);
        Assert.Equal(ReservationsModuleMetadata.ProjectionWorkerGroup, refresh.WorkerGroup);
        ScheduledTaskDefinition dispatch = Assert.Single(
            schedules,
            schedule => schedule.TaskName == DispatchReservationArrivalRemindersPayload.TaskName);
        Assert.Equal(TimeSpan.FromMinutes(1), dispatch.Interval);
        Assert.Equal(ReservationsModuleMetadata.ReminderWorkerGroup, dispatch.WorkerGroup);
    }

    private sealed class FakeRepository(IReadOnlyList<string> scopeIds)
        : IReservationArrivalReminderRepository
    {
        public Task ApplyPropertyAsync(
            ReservationReminderPropertyWriteModel property,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RefreshReservationAsync(
            ReservationReminderSource reservation,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<ReservationArrivalReminderClaimResult> ClaimDueAsync(
            DateTimeOffset nowUtc,
            int batchSize,
            CancellationToken cancellationToken) => Task.FromResult(
                new ReservationArrivalReminderClaimResult(0, []));

        public Task<IReadOnlyList<string>> ListScheduleScopeIdsAsync(
            CancellationToken cancellationToken) => Task.FromResult(scopeIds);
    }
}
