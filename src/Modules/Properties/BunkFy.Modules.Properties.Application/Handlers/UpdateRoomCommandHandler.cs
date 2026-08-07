namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Errors;
using BunkFy.Modules.Properties.Domain.ValueObjects;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class UpdateRoomCommandHandler(
    IRoomRepository repository,
    PropertiesMutationCoordinator mutations,
    PropertyMutationOperationJournal journal,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<UpdateRoomCommand, RoomMutationReceiptDto>
{
    public async Task<Result<RoomMutationReceiptDto>> HandleAsync(
        UpdateRoomCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OperationId == Guid.Empty)
        {
            return Result.Failure<RoomMutationReceiptDto>(
                PropertiesApplicationErrors.ManagementOperationInvalid);
        }

        Result<RoomDefinition> definition = RoomDefinition.Create(
            scopeContext.ScopeId,
            command.Name,
            command.BuildingLabel,
            command.FloorLabel);
        if (definition.IsFailure)
        {
            return Result.Failure<RoomMutationReceiptDto>(definition.Error);
        }

        string fingerprint = RoomMutationFingerprint.ComputeUpdate(
            command.PropertyId,
            command.RoomId,
            command.ExpectedVersion,
            definition.Value);
        Room? room = await mutations
            .AcquireRoomAsync(
                command.RoomId,
                cancellationToken).ConfigureAwait(false);
        if (room is null || room.PropertyId != command.PropertyId)
        {
            return Result.Failure<RoomMutationReceiptDto>(PropertiesDomainErrors.RoomNotFound);
        }

        PropertyMutationReplayDecision<RoomMutationReceiptDto> replay =
            await journal.InspectRoomAsync(
                room.PropertyId,
                PropertyMutationResourceKind.Room,
                room.Id,
                command.OperationId,
                PropertyMutationKind.RoomUpdate,
                command.ExpectedVersion,
                fingerprint,
                cancellationToken).ConfigureAwait(false);
        if (replay.Exists)
        {
            return replay.ToResult();
        }

        Result<RoomDetailsUpdateOutcome> evaluation =
            room.EvaluateDetailsUpdate(
                definition.Value,
                command.ExpectedVersion);
        if (evaluation.IsFailure)
        {
            return Result.Failure<RoomMutationReceiptDto>(evaluation.Error);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        if (evaluation.Value == RoomDetailsUpdateOutcome.Changed)
        {
            bool nameChanged = room.Name != definition.Value.Name;
            if (nameChanged)
            {
                await mutations.AcquireRoomNameAsync(
                    room.PropertyId,
                    definition.Value.Name,
                    cancellationToken).ConfigureAwait(false);
                if (await repository.RoomNameExistsAsync(
                        room.PropertyId,
                        definition.Value.Name.Value,
                        room.Id,
                        cancellationToken).ConfigureAwait(false))
                {
                    return Result.Failure<RoomMutationReceiptDto>(
                        PropertiesDomainErrors.RoomAlreadyExists);
                }
            }

            Result<RoomDetailsUpdateOutcome> update = room.UpdateDetails(
                definition.Value,
                command.ExpectedVersion,
                idGenerator.NewId(),
                nowUtc);
            if (update.IsFailure ||
                update.Value != RoomDetailsUpdateOutcome.Changed)
            {
                return update.IsFailure
                    ? Result.Failure<RoomMutationReceiptDto>(update.Error)
                    : throw new InvalidOperationException(
                        "The Room details update outcome changed while locked.");
            }
        }

        RoomMutationReceiptDto receipt = await journal.RecordRoomAsync(
            room,
            PropertyMutationResourceKind.Room,
            room.Id,
            command.OperationId,
            PropertyMutationKind.RoomUpdate,
            command.ExpectedVersion,
            fingerprint,
            room.Version,
            nowUtc,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(receipt);
    }
}
