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

internal sealed class UpdateBedCommandHandler(
    PropertiesMutationCoordinator mutations,
    PropertyMutationOperationJournal journal,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<UpdateBedCommand, BedMutationReceiptDto>
{
    public async Task<Result<BedMutationReceiptDto>> HandleAsync(
        UpdateBedCommand command,
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

        string fingerprint = BedMutationFingerprint.ComputeUpdate(
            command.PropertyId,
            command.RoomId,
            command.BedId,
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
                PropertyMutationKind.BedUpdate,
                command.ExpectedRoomVersion,
                fingerprint,
                cancellationToken).ConfigureAwait(false);
        if (replay.Exists)
        {
            return replay.ToResult();
        }

        Result<BedDetailsUpdateOutcome> evaluation = room.EvaluateBedUpdate(
            command.BedId,
            label.Value,
            command.ExpectedRoomVersion);
        if (evaluation.IsFailure)
        {
            return Result.Failure<BedMutationReceiptDto>(evaluation.Error);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result<Bed> bedResult = room.UpdateBed(
            command.BedId,
            label.Value.Value,
            command.ExpectedRoomVersion,
            evaluation.Value == BedDetailsUpdateOutcome.Changed
                ? idGenerator.NewId()
                : Guid.Empty,
            nowUtc);
        if (bedResult.IsFailure)
        {
            return Result.Failure<BedMutationReceiptDto>(bedResult.Error);
        }

        BedMutationReceiptDto receipt = await journal.RecordBedAsync(
            room,
            bedResult.Value,
            command.OperationId,
            PropertyMutationKind.BedUpdate,
            command.ExpectedRoomVersion,
            fingerprint,
            nowUtc,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(receipt);
    }
}
