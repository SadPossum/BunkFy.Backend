namespace BunkFy.Modules.Properties.Tests;

using BunkFy.Modules.Properties.Application.Handlers;
using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Entities;
using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Xunit;

[Trait("Category", "Unit")]
public sealed class RoomRetirementFinalizationRequestedHandlerTests
{
    [Fact]
    public async Task Finalization_retires_the_room_and_active_beds_idempotently()
    {
        Guid propertyId = Guid.NewGuid();
        Guid roomId = Guid.NewGuid();
        Room room = Room.Create(
            roomId,
            "tenant-a",
            propertyId,
            "101",
            null,
            null,
            Guid.NewGuid(),
            Now).Value;
        room.AddBed(Guid.NewGuid(), "A", 1, Guid.NewGuid(), Now);
        room.AddBed(Guid.NewGuid(), "B", 2, Guid.NewGuid(), Now);
        room.ClearDomainEvents();
        RecordingOutbox outbox = new();
        RoomRetirementFinalizationRequestedHandler handler = new(
            new FakeRoomRepository(room),
            new RecordingOutboxRegistry(outbox),
            new TestClock(),
            new TestIdGenerator());
        RoomRetirementFinalizationRequestedIntegrationEvent request = new(
            Guid.NewGuid(),
            "tenant-a",
            Now,
            Guid.NewGuid(),
            propertyId,
            roomId);

        await handler.HandleAsync(request, CancellationToken.None);
        room.ClearDomainEvents();
        await handler.HandleAsync(request, CancellationToken.None);

        Assert.Equal(RoomState.Retired, room.Status);
        Assert.All(room.Beds, bed => Assert.Equal(BedState.Retired, bed.Status));
        Assert.Equal(4, room.Version);
        Assert.Collection(
            outbox.Events,
            item => Assert.Equal(request.TopologyChangeId, Assert.IsType<RoomRetirementFinalizedIntegrationEvent>(item).TopologyChangeId),
            item => Assert.Equal(request.TopologyChangeId, Assert.IsType<RoomRetirementFinalizedIntegrationEvent>(item).TopologyChangeId));
    }

    private sealed class FakeRoomRepository(Room room) : IRoomRepository
    {
        public Task AddAsync(Room value, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Room?> GetAsync(Guid roomId, CancellationToken cancellationToken) =>
            Task.FromResult<Room?>(room.Id == roomId ? room : null);

        public Task<bool> HasActiveRoomsAsync(Guid propertyId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> RoomNameExistsAsync(
            Guid propertyId,
            string name,
            Guid? excludingRoomId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class RecordingOutbox : IOutboxWriter
    {
        public string ModuleName => PropertiesModuleMetadata.Name;
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

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }

    private static readonly DateTimeOffset Now = new(2026, 7, 15, 8, 0, 0, TimeSpan.Zero);
}
