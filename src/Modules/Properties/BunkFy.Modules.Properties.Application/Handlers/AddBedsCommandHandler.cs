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

internal sealed class AddBedsCommandHandler(
    PropertiesMutationCoordinator mutations,
    PropertyMutationOperationJournal journal,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<AddBedsCommand, BedBatchMutationReceiptDto>
{
    public async Task<Result<BedBatchMutationReceiptDto>> HandleAsync(
        AddBedsCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OperationId == Guid.Empty)
        {
            return Result.Failure<BedBatchMutationReceiptDto>(
                PropertiesApplicationErrors.ManagementOperationInvalid);
        }

        Result<IReadOnlyCollection<BedLabel>> labels =
            BedMutationInputNormalization.NormalizeBatch(command.Labels);
        if (labels.IsFailure)
        {
            return Result.Failure<BedBatchMutationReceiptDto>(labels.Error);
        }

        string fingerprint = BedMutationFingerprint.ComputeBatchAdd(
            command.PropertyId,
            command.RoomId,
            command.ExpectedRoomVersion,
            labels.Value);
        Room? room = await mutations
            .AcquireRoomAsync(
                command.RoomId,
                cancellationToken).ConfigureAwait(false);
        if (room is null || room.PropertyId != command.PropertyId)
        {
            return Result.Failure<BedBatchMutationReceiptDto>(PropertiesDomainErrors.RoomNotFound);
        }

        PropertyMutationReplayDecision<BedBatchMutationReceiptDto> replay =
            await journal.InspectBedBatchAsync(
                room.PropertyId,
                room.Id,
                command.OperationId,
                PropertyMutationKind.BedBatchAdd,
                command.ExpectedRoomVersion,
                fingerprint,
                cancellationToken).ConfigureAwait(false);
        if (replay.Exists)
        {
            return replay.ToResult();
        }

        Result evaluation = room.EvaluateBedAdditions(
            labels.Value,
            command.ExpectedRoomVersion);
        if (evaluation.IsFailure)
        {
            return Result.Failure<BedBatchMutationReceiptDto>(evaluation.Error);
        }

        BedAdditionDefinition[] additions = labels.Value
            .Select(label => new BedAdditionDefinition(
                idGenerator.NewId(),
                label.Value,
                idGenerator.NewId()))
            .ToArray();
        DateTimeOffset nowUtc = clock.UtcNow;
        Result<IReadOnlyCollection<Bed>> result = room.AddBeds(
            additions,
            command.ExpectedRoomVersion,
            nowUtc);
        if (result.IsFailure)
        {
            return Result.Failure<BedBatchMutationReceiptDto>(result.Error);
        }

        BedBatchMutationReceiptDto receipt = await journal.RecordBedBatchAsync(
            room,
            result.Value.Count,
            command.OperationId,
            command.ExpectedRoomVersion,
            fingerprint,
            nowUtc,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(receipt);
    }
}
