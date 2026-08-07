namespace BunkFy.Modules.Properties.Tests;

using BunkFy.Modules.Properties.Application;
using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Application.Handlers;
using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Entities;
using BunkFy.Modules.Properties.Domain.Errors;
using BunkFy.Modules.Properties.Domain.Events;
using BunkFy.Modules.Properties.Domain.ValueObjects;
using Gma.Framework.Results;
using Xunit;
using static BedMutationCommandTestSupport;

[Trait("Category", "Unit")]
public sealed class BedUpdateMutationCommandHandlerTests
{
    [Fact]
    public async Task Exact_update_replay_is_immutable_after_a_later_update()
    {
        Room room = CreateRoom();
        Bed bed = AddBed(room, "A");
        RecordingPropertyMutationOperationRepository operations = new();
        CountingIdGenerator ids = new();
        UpdateBedCommandHandler handler = CreateUpdateHandler(
            new RecordingRoomRepository(room),
            operations,
            ids);
        Guid operationId = Guid.NewGuid();

        Result<BedMutationReceiptDto> first = await handler.HandleAsync(
            new UpdateBedCommand(
                operationId,
                room.PropertyId,
                room.Id,
                bed.Id,
                room.Version,
                " B "),
            CancellationToken.None);
        Assert.True(first.IsSuccess);
        Assert.True(room.UpdateBed(
            bed.Id,
            "C",
            room.Version,
            Guid.NewGuid(),
            Now.AddMinutes(1)).IsSuccess);
        room.ClearDomainEvents();

        Result<BedMutationReceiptDto> replay = await handler.HandleAsync(
            new UpdateBedCommand(
                operationId,
                room.PropertyId,
                room.Id,
                bed.Id,
                ExpectedRoomVersion: 2,
                "B"),
            CancellationToken.None);

        Assert.True(replay.IsSuccess);
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(2, replay.Value.Version);
        Assert.Equal(3, replay.Value.RoomVersion);
        Assert.Equal(4, room.Version);
        Assert.Equal("C", bed.Label.Value);
        Assert.Empty(room.DomainEvents);
        Assert.Equal(1, ids.Count);
        Assert.Single(operations.Added);
    }

    [Fact]
    public async Task Unchanged_update_records_a_receipt_without_versions_or_event()
    {
        Room room = CreateRoom();
        Bed bed = AddBed(room, "A");
        RecordingPropertyMutationOperationRepository operations = new();
        CountingIdGenerator ids = new();
        UpdateBedCommandHandler handler = CreateUpdateHandler(
            new RecordingRoomRepository(room),
            operations,
            ids);

        Result<BedMutationReceiptDto> result = await handler.HandleAsync(
            new UpdateBedCommand(
                Guid.NewGuid(),
                room.PropertyId,
                room.Id,
                bed.Id,
                room.Version,
                " A "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Version);
        Assert.Equal(2, result.Value.RoomVersion);
        Assert.Equal(1, bed.Version);
        Assert.Equal(2, room.Version);
        Assert.Empty(room.DomainEvents);
        Assert.Equal(0, ids.Count);
        PropertyMutationOperationRecord operation = Assert.Single(
            operations.Added);
        Assert.Equal(PropertyMutationKind.BedUpdate, operation.Kind);
        Assert.Equal(bed.Id, operation.ResultBedId);
        Assert.Equal(1, operation.ResultVersion);
        Assert.Equal(2, operation.ResultResourceVersion);
    }

    [Fact]
    public async Task Committed_update_rejects_changed_label_and_bed_target_reuse()
    {
        Room room = CreateRoom();
        Bed firstBed = AddBed(room, "A");
        Bed secondBed = AddBed(room, "B");
        RecordingPropertyMutationOperationRepository operations = new();
        UpdateBedCommandHandler handler = CreateUpdateHandler(
            new RecordingRoomRepository(room),
            operations);
        Guid operationId = Guid.NewGuid();
        long expectedVersion = room.Version;

        Result<BedMutationReceiptDto> first = await handler.HandleAsync(
            new UpdateBedCommand(
                operationId,
                room.PropertyId,
                room.Id,
                firstBed.Id,
                expectedVersion,
                "A1"),
            CancellationToken.None);
        Result<BedMutationReceiptDto> changed = await handler.HandleAsync(
            new UpdateBedCommand(
                operationId,
                room.PropertyId,
                room.Id,
                firstBed.Id,
                expectedVersion,
                "A2"),
            CancellationToken.None);
        Result<BedMutationReceiptDto> otherBed = await handler.HandleAsync(
            new UpdateBedCommand(
                operationId,
                room.PropertyId,
                room.Id,
                secondBed.Id,
                expectedVersion,
                "B1"),
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(
            PropertiesApplicationErrors.ManagementOperationConflict,
            changed.Error);
        Assert.Equal(
            PropertiesApplicationErrors.ManagementOperationConflict,
            otherBed.Error);
        Assert.Equal("A1", firstBed.Label.Value);
        Assert.Equal("B", secondBed.Label.Value);
        Assert.Single(operations.Added);
    }

    [Fact]
    public async Task Failed_duplicate_update_can_reuse_the_operation_after_correction()
    {
        Room room = CreateRoom();
        Bed firstBed = AddBed(room, "A");
        AddBed(room, "B");
        RecordingPropertyMutationOperationRepository operations = new();
        CountingIdGenerator ids = new();
        UpdateBedCommandHandler handler = CreateUpdateHandler(
            new RecordingRoomRepository(room),
            operations,
            ids);
        Guid operationId = Guid.NewGuid();

        Result<BedMutationReceiptDto> duplicate = await handler.HandleAsync(
            new UpdateBedCommand(
                operationId,
                room.PropertyId,
                room.Id,
                firstBed.Id,
                room.Version,
                "B"),
            CancellationToken.None);
        Result<BedMutationReceiptDto> corrected = await handler.HandleAsync(
            new UpdateBedCommand(
                operationId,
                room.PropertyId,
                room.Id,
                firstBed.Id,
                room.Version,
                "C"),
            CancellationToken.None);

        Assert.Equal(PropertiesDomainErrors.BedAlreadyExists, duplicate.Error);
        Assert.True(corrected.IsSuccess);
        Assert.Equal("C", firstBed.Label.Value);
        Assert.Equal(1, ids.Count);
        Assert.Single(operations.Added);
    }

    [Fact]
    public async Task Bed_update_rejects_a_room_from_another_property()
    {
        Room room = CreateRoom();
        Bed bed = AddBed(room, "A");
        RecordingPropertyMutationOperationRepository operations = new();
        CountingIdGenerator ids = new();
        UpdateBedCommandHandler handler = CreateUpdateHandler(
            new RecordingRoomRepository(room),
            operations,
            ids);

        Result<BedMutationReceiptDto> result = await handler.HandleAsync(
            new UpdateBedCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                room.Id,
                bed.Id,
                room.Version,
                "B"),
            CancellationToken.None);

        Assert.Equal(PropertiesDomainErrors.RoomNotFound, result.Error);
        Assert.Equal("A", bed.Label.Value);
        Assert.Equal(0, operations.ReadCount);
        Assert.Equal(0, ids.Count);
    }

    [Fact]
    public async Task Admission_is_checked_before_an_exact_replay()
    {
        Room room = CreateRoom();
        Bed bed = AddBed(room, "A");
        Guid operationId = Guid.NewGuid();
        string fingerprint = BedMutationFingerprint.ComputeUpdate(
            room.PropertyId,
            room.Id,
            bed.Id,
            room.Version,
            BedLabel.Create("B").Value);
        PropertyMutationOperationRecord existing =
            PropertyMutationOperationRecord.ForBed(
                operationId,
                room.ScopeId,
                room.PropertyId,
                room.Id,
                PropertyMutationKind.BedUpdate,
                room.Version,
                fingerprint,
                new BedMutationReceiptDto(
                    room.PropertyId,
                    room.Id,
                    bed.Id,
                    BedStatus.Active,
                    bed.Version + 1,
                    room.Version + 1),
                Now);
        RecordingPropertyMutationOperationRepository operations = new(existing);
        UpdateBedCommandHandler handler = CreateUpdateHandler(
            new RecordingRoomRepository(room),
            operations,
            operationLock: new DenyingOperationLock());

        Result<BedMutationReceiptDto> result = await handler.HandleAsync(
            new UpdateBedCommand(
                operationId,
                room.PropertyId,
                room.Id,
                bed.Id,
                room.Version,
                "B"),
            CancellationToken.None);

        Assert.Equal(PropertiesDomainErrors.RoomNotFound, result.Error);
        Assert.Equal(0, operations.ReadCount);
    }
}
