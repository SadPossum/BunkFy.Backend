namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Entities;
using BunkFy.Modules.Properties.Domain.Errors;
using BunkFy.Modules.Properties.Domain.ValueObjects;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class AddBedCommandHandler(
    PropertiesMutationCoordinator mutations,
    PropertyMutationOperationJournal journal,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<AddBedCommand, BedMutationReceiptDto>
{
    public async Task<Result<BedMutationReceiptDto>> HandleAsync(
        AddBedCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OperationId == Guid.Empty)
        {
            return Result.Failure<BedMutationReceiptDto>(
                PropertiesApplicationErrors.ManagementOperationInvalid);
        }

        Result<BedLabel> label =
            BedMutationInputNormalization.NormalizeLabel(command.Label);
        if (label.IsFailure)
        {
            return Result.Failure<BedMutationReceiptDto>(label.Error);
        }

        string fingerprint = BedMutationFingerprint.ComputeAdd(
            command.PropertyId,
            command.RoomId,
            command.ExpectedRoomVersion,
            label.Value);
        Room? room = await mutations
            .AcquireRoomAsync(
                command.RoomId,
                cancellationToken).ConfigureAwait(false);
        if (room is null || room.PropertyId != command.PropertyId)
        {
            return Result.Failure<BedMutationReceiptDto>(PropertiesDomainErrors.RoomNotFound);
        }

        PropertyMutationReplayDecision<BedMutationReceiptDto> replay =
            await journal.InspectBedAsync(
                room.PropertyId,
                room.Id,
                command.OperationId,
                PropertyMutationKind.BedAdd,
                command.ExpectedRoomVersion,
                fingerprint,
                cancellationToken).ConfigureAwait(false);
        if (replay.Exists)
        {
            return replay.ToResult();
        }

        Result evaluation = room.EvaluateBedAdditions(
            [label.Value],
            command.ExpectedRoomVersion);
        if (evaluation.IsFailure)
        {
            return Result.Failure<BedMutationReceiptDto>(evaluation.Error);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result<Bed> bedResult = room.AddBed(
            idGenerator.NewId(),
            label.Value.Value,
            command.ExpectedRoomVersion,
            idGenerator.NewId(),
            nowUtc);
        if (bedResult.IsFailure)
        {
            return Result.Failure<BedMutationReceiptDto>(bedResult.Error);
        }

        BedMutationReceiptDto receipt = await journal.RecordBedAsync(
            room,
            bedResult.Value,
            command.OperationId,
            PropertyMutationKind.BedAdd,
            command.ExpectedRoomVersion,
            fingerprint,
            nowUtc,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(receipt);
    }
}
