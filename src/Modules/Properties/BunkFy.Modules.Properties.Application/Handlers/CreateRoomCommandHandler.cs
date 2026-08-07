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

internal sealed class CreateRoomCommandHandler(
    IRoomRepository roomRepository,
    PropertiesMutationCoordinator mutations,
    PropertyMutationOperationJournal journal,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<CreateRoomCommand, RoomMutationReceiptDto>
{
    public async Task<Result<RoomMutationReceiptDto>> HandleAsync(
        CreateRoomCommand command,
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

        string fingerprint = RoomMutationFingerprint.ComputeCreate(
            command.PropertyId,
            command.ExpectedPropertyVersion,
            definition.Value);
        Property? property = await mutations
            .AcquirePropertyAsync(
                command.PropertyId,
                cancellationToken).ConfigureAwait(false);
        if (property is null)
        {
            return Result.Failure<RoomMutationReceiptDto>(PropertiesDomainErrors.PropertyNotFound);
        }

        PropertyMutationReplayDecision<RoomMutationReceiptDto> replay =
            await journal.InspectRoomAsync(
                property.Id,
                PropertyMutationResourceKind.Property,
                property.Id,
                command.OperationId,
                PropertyMutationKind.RoomCreate,
                command.ExpectedPropertyVersion,
                fingerprint,
                cancellationToken).ConfigureAwait(false);
        if (replay.Exists)
        {
            return replay.ToResult();
        }

        Result registrationEvaluation = property.EvaluateRoomRegistration(
            command.ExpectedPropertyVersion);
        if (registrationEvaluation.IsFailure)
        {
            return Result.Failure<RoomMutationReceiptDto>(
                registrationEvaluation.Error);
        }

        await mutations.AcquireRoomNameAsync(
            command.PropertyId,
            definition.Value.Name,
            cancellationToken).ConfigureAwait(false);
        if (await roomRepository.RoomNameExistsAsync(
                command.PropertyId,
                definition.Value.Name.Value,
                null,
                cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<RoomMutationReceiptDto>(
                PropertiesDomainErrors.RoomAlreadyExists);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result<Room> roomResult = Room.Create(
            idGenerator.NewId(),
            definition.Value.ScopeId,
            command.PropertyId,
            definition.Value.Name.Value,
            definition.Value.BuildingLabel?.Value,
            definition.Value.FloorLabel?.Value,
            idGenerator.NewId(),
            nowUtc);
        if (roomResult.IsFailure)
        {
            return Result.Failure<RoomMutationReceiptDto>(roomResult.Error);
        }

        Room room = roomResult.Value;
        Result registrationResult = property.RegisterRoom(command.ExpectedPropertyVersion);
        if (registrationResult.IsFailure)
        {
            return Result.Failure<RoomMutationReceiptDto>(registrationResult.Error);
        }

        await roomRepository.AddAsync(room, cancellationToken).ConfigureAwait(false);

        RoomMutationReceiptDto receipt = await journal.RecordRoomAsync(
            room,
            PropertyMutationResourceKind.Property,
            property.Id,
            command.OperationId,
            PropertyMutationKind.RoomCreate,
            command.ExpectedPropertyVersion,
            fingerprint,
            property.Version,
            nowUtc,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(receipt);
    }
}
