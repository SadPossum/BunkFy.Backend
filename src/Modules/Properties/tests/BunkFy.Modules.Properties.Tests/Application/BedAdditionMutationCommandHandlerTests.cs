namespace BunkFy.Modules.Properties.Tests;

using BunkFy.Modules.Properties.Application;
using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Application.Handlers;
using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Errors;
using BunkFy.Modules.Properties.Domain.Events;
using Gma.Framework.Results;
using Xunit;
using static BedMutationCommandTestSupport;

[Trait("Category", "Unit")]
public sealed class BedAdditionMutationCommandHandlerTests
{
    [Fact]
    public async Task Exact_single_add_replay_returns_the_generated_receipt_once()
    {
        Room room = CreateRoom();
        RecordingPropertyMutationOperationRepository operations = new();
        Guid bedId = Guid.NewGuid();
        CountingIdGenerator ids = new(bedId, Guid.NewGuid());
        AddBedCommandHandler handler = CreateAddHandler(
            new RecordingRoomRepository(room),
            operations,
            ids);
        Guid operationId = Guid.NewGuid();

        Result<BedMutationReceiptDto> first = await handler.HandleAsync(
            new AddBedCommand(
                operationId,
                room.PropertyId,
                room.Id,
                room.Version,
                " A "),
            CancellationToken.None);
        Result<BedMutationReceiptDto> replay = await handler.HandleAsync(
            new AddBedCommand(
                operationId,
                room.PropertyId,
                room.Id,
                ExpectedRoomVersion: 1,
                "A"),
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(bedId, replay.Value.BedId);
        Assert.Equal(2, room.Version);
        Assert.Single(room.Beds);
        Assert.IsType<BedAddedDomainEvent>(Assert.Single(room.DomainEvents));
        Assert.Equal(2, ids.Count);
        PropertyMutationOperationRecord operation = Assert.Single(
            operations.Added);
        Assert.Equal(PropertyMutationResourceKind.Room, operation.ResourceKind);
        Assert.Equal(room.Id, operation.ResourceId);
        Assert.Equal(PropertyMutationKind.BedAdd, operation.Kind);
        Assert.Equal(bedId, operation.ResultBedId);
        Assert.Equal(BedStatus.Active, operation.ResultBedStatus);
        Assert.Equal(1, operation.ResultVersion);
        Assert.Equal(2, operation.ResultResourceVersion);
    }

    [Fact]
    public async Task Exact_batch_replay_preserves_ordered_results_and_events()
    {
        Room room = CreateRoom();
        RecordingPropertyMutationOperationRepository operations = new();
        CountingIdGenerator ids = new();
        AddBedsCommandHandler handler = CreateBatchHandler(
            new RecordingRoomRepository(room),
            operations,
            ids);
        Guid operationId = Guid.NewGuid();

        Result<BedBatchMutationReceiptDto> first = await handler.HandleAsync(
            new AddBedsCommand(
                operationId,
                room.PropertyId,
                room.Id,
                room.Version,
                [" 1 ", "2", " 3"]),
            CancellationToken.None);
        Result<BedBatchMutationReceiptDto> replay = await handler.HandleAsync(
            new AddBedsCommand(
                operationId,
                room.PropertyId,
                room.Id,
                ExpectedRoomVersion: 1,
                ["1", "2", "3"]),
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(3, replay.Value.AffectedBedCount);
        Assert.Equal(4, replay.Value.RoomVersion);
        Assert.Equal(["1", "2", "3"], room.Beds.Select(bed => bed.Label.Value));
        Assert.Equal(3, room.DomainEvents.OfType<BedAddedDomainEvent>().Count());
        Assert.Equal(6, ids.Count);
        PropertyMutationOperationRecord operation = Assert.Single(
            operations.Added);
        Assert.Equal(PropertyMutationKind.BedBatchAdd, operation.Kind);
        Assert.Equal(3, operation.ResultAffectedBedCount);
        Assert.Equal(4, operation.ResultVersion);
        Assert.Equal(4, operation.ResultResourceVersion);
    }

    [Fact]
    public async Task Committed_add_operation_rejects_changed_and_cross_kind_reuse()
    {
        Room room = CreateRoom();
        RecordingPropertyMutationOperationRepository operations = new();
        RecordingRoomRepository rooms = new(room);
        AddBedCommandHandler add = CreateAddHandler(rooms, operations);
        AddBedsCommandHandler batch = CreateBatchHandler(rooms, operations);
        Guid operationId = Guid.NewGuid();

        Result<BedMutationReceiptDto> first = await add.HandleAsync(
            new AddBedCommand(
                operationId,
                room.PropertyId,
                room.Id,
                room.Version,
                "A"),
            CancellationToken.None);
        Result<BedMutationReceiptDto> changed = await add.HandleAsync(
            new AddBedCommand(
                operationId,
                room.PropertyId,
                room.Id,
                ExpectedRoomVersion: 1,
                "B"),
            CancellationToken.None);
        Result<BedBatchMutationReceiptDto> otherKind = await batch.HandleAsync(
            new AddBedsCommand(
                operationId,
                room.PropertyId,
                room.Id,
                ExpectedRoomVersion: 1,
                ["A"]),
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(
            PropertiesApplicationErrors.ManagementOperationConflict,
            changed.Error);
        Assert.Equal(
            PropertiesApplicationErrors.ManagementOperationConflict,
            otherKind.Error);
        Assert.Single(room.Beds);
        Assert.Single(operations.Added);
    }

    [Fact]
    public async Task Failed_duplicate_add_can_reuse_the_operation_after_correction()
    {
        Room room = CreateRoom();
        AddBed(room, "A");
        RecordingPropertyMutationOperationRepository operations = new();
        CountingIdGenerator ids = new();
        AddBedCommandHandler handler = CreateAddHandler(
            new RecordingRoomRepository(room),
            operations,
            ids);
        Guid operationId = Guid.NewGuid();

        Result<BedMutationReceiptDto> duplicate = await handler.HandleAsync(
            new AddBedCommand(
                operationId,
                room.PropertyId,
                room.Id,
                room.Version,
                " A "),
            CancellationToken.None);
        Result<BedMutationReceiptDto> corrected = await handler.HandleAsync(
            new AddBedCommand(
                operationId,
                room.PropertyId,
                room.Id,
                room.Version,
                "B"),
            CancellationToken.None);

        Assert.Equal(PropertiesDomainErrors.BedAlreadyExists, duplicate.Error);
        Assert.True(corrected.IsSuccess);
        Assert.Equal(2, ids.Count);
        Assert.Single(operations.Added);
        Assert.Equal(["A", "B"], room.Beds.Select(bed => bed.Label.Value));
    }

    [Fact]
    public async Task Invalid_batch_is_rejected_before_loading_or_allocating()
    {
        Room room = CreateRoom();
        RecordingRoomRepository rooms = new(room);
        CountingIdGenerator ids = new();
        AddBedsCommandHandler handler = CreateBatchHandler(
            rooms,
            new RecordingPropertyMutationOperationRepository(),
            ids);

        Result<BedBatchMutationReceiptDto> result = await handler.HandleAsync(
            new AddBedsCommand(
                Guid.NewGuid(),
                room.PropertyId,
                room.Id,
                room.Version,
                [" A ", "A"]),
            CancellationToken.None);

        Assert.Equal(PropertiesDomainErrors.BedAlreadyExists, result.Error);
        Assert.Equal(0, rooms.GetCount);
        Assert.Equal(0, ids.Count);
    }

    [Fact]
    public async Task Bed_additions_reject_a_room_from_another_property()
    {
        Room room = CreateRoom();
        RecordingPropertyMutationOperationRepository operations = new();
        CountingIdGenerator ids = new();
        RecordingRoomRepository rooms = new(room);
        AddBedCommandHandler add = CreateAddHandler(rooms, operations, ids);
        AddBedsCommandHandler batch = CreateBatchHandler(
            rooms,
            operations,
            ids);
        Guid otherPropertyId = Guid.NewGuid();

        Result<BedMutationReceiptDto> singleResult = await add.HandleAsync(
            new AddBedCommand(
                Guid.NewGuid(),
                otherPropertyId,
                room.Id,
                room.Version,
                "A"),
            CancellationToken.None);
        Result<BedBatchMutationReceiptDto> batchResult =
            await batch.HandleAsync(
                new AddBedsCommand(
                    Guid.NewGuid(),
                    otherPropertyId,
                    room.Id,
                    room.Version,
                    ["A", "B"]),
                CancellationToken.None);

        Assert.Equal(PropertiesDomainErrors.RoomNotFound, singleResult.Error);
        Assert.Equal(PropertiesDomainErrors.RoomNotFound, batchResult.Error);
        Assert.Empty(room.Beds);
        Assert.Equal(0, operations.ReadCount);
        Assert.Equal(0, ids.Count);
    }

    [Fact]
    public async Task The_same_operation_id_is_isolated_between_rooms()
    {
        Guid propertyId = Guid.NewGuid();
        Room firstRoom = CreateRoom(propertyId, "4A");
        Room secondRoom = CreateRoom(propertyId, "4B");
        RecordingPropertyMutationOperationRepository operations = new();
        AddBedCommandHandler handler = CreateAddHandler(
            new RecordingRoomRepository(firstRoom, secondRoom),
            operations);
        Guid operationId = Guid.NewGuid();

        Result<BedMutationReceiptDto> first = await handler.HandleAsync(
            new AddBedCommand(
                operationId,
                propertyId,
                firstRoom.Id,
                firstRoom.Version,
                "A"),
            CancellationToken.None);
        Result<BedMutationReceiptDto> second = await handler.HandleAsync(
            new AddBedCommand(
                operationId,
                propertyId,
                secondRoom.Id,
                secondRoom.Version,
                "A"),
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, operations.Added.Count);
    }
}
