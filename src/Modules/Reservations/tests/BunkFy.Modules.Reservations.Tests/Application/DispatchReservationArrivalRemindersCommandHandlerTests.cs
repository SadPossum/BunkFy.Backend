namespace BunkFy.Modules.Reservations.Tests;

using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Handlers;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Time;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DispatchReservationArrivalRemindersCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 16, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Dispatch_publishes_only_the_minimized_version_two_contract()
    {
        Guid reminderId = Guid.NewGuid();
        Guid reservationId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        var repository = new StubReminderRepository(new ReservationArrivalReminderDispatch(
            reminderId,
            "tenant-a",
            reservationId,
            propertyId,
            new DateOnly(2026, 7, 16),
            new TimeOnly(15, 30),
            "Europe/Moscow",
            3));
        var outbox = new RecordingOutbox();
        var handler = new DispatchReservationArrivalRemindersCommandHandler(
            repository,
            new RecordingOutboxRegistry(outbox),
            new TestClock());

        Gma.Framework.Results.Result<ReservationArrivalReminderDispatchBatchResult> result =
            await handler.HandleAsync(new(100), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(1, result.Value.DispatchedCount);
        ReservationArrivalReminderDueIntegrationEventV2 integrationEvent =
            Assert.IsType<ReservationArrivalReminderDueIntegrationEventV2>(Assert.Single(outbox.Events));
        Assert.Equal(ReservationArrivalReminderDueIntegrationEventV2.EventVersion, integrationEvent.Version);
        Assert.Equal(reminderId, integrationEvent.EventId);
        Assert.Equal(reservationId, integrationEvent.ReservationId);
        Assert.DoesNotContain(
            integrationEvent.GetType().GetProperties(),
            property => property.Name is "PrimaryGuestName" or "Email" or "Phone" or "Notes");
    }

    private sealed class StubReminderRepository(ReservationArrivalReminderDispatch dispatch)
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
            CancellationToken cancellationToken) =>
            Task.FromResult(new ReservationArrivalReminderClaimResult(1, [dispatch]));

        public Task<IReadOnlyList<string>> ListScheduleScopeIdsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(["tenant-a"]);
    }

    private sealed class RecordingOutbox : IOutboxWriter
    {
        public string ModuleName => ReservationsModuleMetadata.Name;
        public List<IIntegrationEvent> Events { get; } = [];

        public Task EnqueueAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
            where TEvent : IIntegrationEvent
        {
            this.Events.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOutboxRegistry(RecordingOutbox outbox) : IOutboxWriterRegistry
    {
        public IOutboxWriter GetRequired(string moduleName) => outbox;
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
