namespace BunkFy.Modules.Properties.Tests;

using System.Globalization;
using BunkFy.Modules.Properties.Application;
using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Aggregates;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AddBedsCommandHandlerTests
{
    [Fact]
    public async Task Batch_add_returns_one_receipt_for_the_atomic_mutation()
    {
        Room room = CreateRoom();
        FakeRoomRepository repository = new(room);
        ICommandHandler<AddBedsCommand, BedBatchMutationReceiptDto> handler = CreateHandler(repository);

        Result<BedBatchMutationReceiptDto> result = await handler.HandleAsync(
            new AddBedsCommand(room.PropertyId, room.Id, room.Version, ["1", "2", "3"]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.AffectedBedCount);
        Assert.Equal(4, result.Value.RoomVersion);
        Assert.Equal(3, room.Beds.Count);
        Assert.Equal(1, repository.GetCalls);
    }

    [Fact]
    public async Task Oversized_batch_is_rejected_before_loading_the_aggregate()
    {
        Room room = CreateRoom();
        FakeRoomRepository repository = new(room);
        ICommandHandler<AddBedsCommand, BedBatchMutationReceiptDto> handler = CreateHandler(repository);

        Result<BedBatchMutationReceiptDto> result = await handler.HandleAsync(
            new AddBedsCommand(
                room.PropertyId,
                room.Id,
                room.Version,
                Enumerable.Range(1, PropertiesContractLimits.MaximumBedsPerBatch + 1)
                    .Select(value => value.ToString(CultureInfo.InvariantCulture))
                    .ToArray()),
            CancellationToken.None);

        Assert.Equal(PropertiesApplicationErrors.BedBatchTooLarge, result.Error);
        Assert.Equal(0, repository.GetCalls);
        Assert.Empty(room.Beds);
    }

    private static ICommandHandler<AddBedsCommand, BedBatchMutationReceiptDto> CreateHandler(
        FakeRoomRepository repository)
    {
        ServiceCollection services = new();
        services.AddSingleton<IRoomRepository>(repository);
        services.AddSingleton<ISystemClock>(new TestClock());
        services.AddSingleton<IIdGenerator>(new TestIdGenerator());
        services.AddPropertiesApplication();
        return services.BuildServiceProvider()
            .GetRequiredService<ICommandHandler<AddBedsCommand, BedBatchMutationReceiptDto>>();
    }

    private static Room CreateRoom() =>
        Room.Create(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            "101",
            null,
            null,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow).Value;

    private sealed class FakeRoomRepository(Room room) : IRoomRepository
    {
        public int GetCalls { get; private set; }

        public Task AddAsync(Room value, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Room?> GetAsync(Guid roomId, CancellationToken cancellationToken)
        {
            this.GetCalls++;
            return Task.FromResult<Room?>(room.Id == roomId ? room : null);
        }

        public Task<bool> HasActiveRoomsAsync(Guid propertyId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> RoomNameExistsAsync(
            Guid propertyId,
            string name,
            Guid? excludingRoomId,
            CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.CreateVersion7();
    }
}
