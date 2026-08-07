namespace BunkFy.Modules.Properties.Tests;

using BunkFy.Modules.Properties.Application.Handlers;
using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Entities;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal static class BedMutationCommandTestSupport
{
    public const string TenantId = "tenant-a";

    public static Room CreateRoom(
        Guid? propertyId = null,
        string name = "4A")
    {
        Room room = Room.Create(
            Guid.NewGuid(),
            TenantId,
            propertyId ?? Guid.NewGuid(),
            name,
            null,
            null,
            Guid.NewGuid(),
            Now).Value;
        room.ClearDomainEvents();
        return room;
    }

    public static Bed AddBed(Room room, string label)
    {
        Bed bed = room.AddBed(
            Guid.NewGuid(),
            label,
            room.Version,
            Guid.NewGuid(),
            Now).Value;
        room.ClearDomainEvents();
        return bed;
    }

    public static AddBedCommandHandler CreateAddHandler(
        RecordingRoomRepository rooms,
        RecordingPropertyMutationOperationRepository operations,
        CountingIdGenerator? ids = null,
        IPropertiesOperationLock? operationLock = null) => new(
            CreateCoordinator(rooms, operationLock),
            new PropertyMutationOperationJournal(operations),
            new TestClock(),
            ids ?? new CountingIdGenerator());

    public static AddBedsCommandHandler CreateBatchHandler(
        RecordingRoomRepository rooms,
        RecordingPropertyMutationOperationRepository operations,
        CountingIdGenerator? ids = null,
        IPropertiesOperationLock? operationLock = null) => new(
            CreateCoordinator(rooms, operationLock),
            new PropertyMutationOperationJournal(operations),
            new TestClock(),
            ids ?? new CountingIdGenerator());

    public static UpdateBedCommandHandler CreateUpdateHandler(
        RecordingRoomRepository rooms,
        RecordingPropertyMutationOperationRepository operations,
        CountingIdGenerator? ids = null,
        IPropertiesOperationLock? operationLock = null) => new(
            CreateCoordinator(rooms, operationLock),
            new PropertyMutationOperationJournal(operations),
            new TestClock(),
            ids ?? new CountingIdGenerator());

    private static PropertiesMutationCoordinator CreateCoordinator(
        RecordingRoomRepository rooms,
        IPropertiesOperationLock? operationLock) =>
        PropertiesMutationTestSupport.Create(
            rooms: rooms,
            operationLock: operationLock,
            scopeContext: new TestScopeContext());

    public sealed class RecordingRoomRepository(params Room[] initial)
        : IRoomRepository
    {
        private readonly List<Room> rooms = [.. initial];

        public int GetCount { get; private set; }

        public Task AddAsync(
            Room room,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Room?> GetAsync(
            Guid roomId,
            CancellationToken cancellationToken)
        {
            this.GetCount++;
            return Task.FromResult(
                this.rooms.SingleOrDefault(room => room.Id == roomId));
        }

        public Task<bool> HasActiveRoomsAsync(
            Guid propertyId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> RoomNameExistsAsync(
            Guid propertyId,
            string name,
            Guid? excludingRoomId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    public sealed class CountingIdGenerator(params Guid[] values)
        : IIdGenerator
    {
        private readonly Queue<Guid> values = new(values);

        public int Count { get; private set; }

        public Guid NewId()
        {
            this.Count++;
            return this.values.TryDequeue(out Guid value)
                ? value
                : Guid.NewGuid();
        }
    }

    public sealed class DenyingOperationLock : IPropertiesOperationLock
    {
        public Task<bool> TryAcquirePropertyAsync(
            string tenantId,
            Guid propertyId,
            CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<bool> TryAcquireRoomAsync(
            string tenantId,
            Guid roomId,
            CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string? ScopeId => TenantId;
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    public static readonly DateTimeOffset Now = new(
        2026,
        8,
        7,
        21,
        0,
        0,
        TimeSpan.Zero);
}
