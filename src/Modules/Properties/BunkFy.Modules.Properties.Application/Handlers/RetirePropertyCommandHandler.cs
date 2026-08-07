namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Errors;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class RetirePropertyCommandHandler(
    PropertiesMutationCoordinator mutations,
    PropertyMutationOperationJournal journal,
    IRoomRepository roomRepository,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<RetirePropertyCommand, PropertyMutationReceiptDto>
{
    public async Task<Result<PropertyMutationReceiptDto>> HandleAsync(
        RetirePropertyCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OperationId == Guid.Empty)
        {
            return Result.Failure<PropertyMutationReceiptDto>(
                PropertiesApplicationErrors.ManagementOperationInvalid);
        }

        if (!command.Confirmed)
        {
            return Result.Failure<PropertyMutationReceiptDto>(
                PropertiesApplicationErrors.ConfirmationRequired);
        }

        Result<PropertyMutationActor> actorResult =
            PropertyMutationActor.Optional(command.ActorId);
        if (actorResult.IsFailure)
        {
            return Result.Failure<PropertyMutationReceiptDto>(
                actorResult.Error);
        }

        PropertyMutationActor actor = actorResult.Value;
        string fingerprint =
            PropertyLifecycleMutationFingerprint.ComputeRetirement(
                command.PropertyId,
                command.ExpectedVersion);
        Property? property = await mutations
            .AcquirePropertyAsync(
                command.PropertyId,
                cancellationToken).ConfigureAwait(false);
        if (property is null)
        {
            return Result.Failure<PropertyMutationReceiptDto>(
                PropertiesDomainErrors.PropertyNotFound);
        }

        PropertyMutationReplayDecision replay = await journal.InspectAsync(
            property,
            command.OperationId,
            PropertyMutationKind.Retirement,
            command.ExpectedVersion,
            fingerprint,
            cancellationToken).ConfigureAwait(false);
        if (replay.Exists)
        {
            return replay.ToResult();
        }

        Result precondition = property.EvaluateRetirement(
            command.ExpectedVersion);
        if (precondition.IsFailure)
        {
            return Result.Failure<PropertyMutationReceiptDto>(
                precondition.Error);
        }

        if (await roomRepository.HasActiveRoomsAsync(
                property.Id,
                cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<PropertyMutationReceiptDto>(
                PropertiesDomainErrors.PropertyHasActiveRooms);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result result = property.Retire(
            command.ExpectedVersion,
            idGenerator.NewId(),
            nowUtc,
            actor.Value);
        if (result.IsFailure)
        {
            return Result.Failure<PropertyMutationReceiptDto>(result.Error);
        }

        PropertyMutationReceiptDto receipt = await journal.RecordAsync(
            property,
            command.OperationId,
            PropertyMutationKind.Retirement,
            command.ExpectedVersion,
            fingerprint,
            nowUtc,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(receipt);
    }
}
