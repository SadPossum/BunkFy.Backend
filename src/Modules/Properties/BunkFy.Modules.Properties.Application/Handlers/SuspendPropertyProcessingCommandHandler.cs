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

internal sealed class SuspendPropertyProcessingCommandHandler(
    PropertiesMutationCoordinator mutations,
    PropertyMutationOperationJournal journal,
    IPropertyGovernanceRevisionWriter revisions,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<
        SuspendPropertyProcessingCommand,
        PropertyMutationReceiptDto>
{
    public async Task<Result<PropertyMutationReceiptDto>> HandleAsync(
        SuspendPropertyProcessingCommand command,
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
            PropertyMutationActor.Required(command.ActorId);
        if (actorResult.IsFailure)
        {
            return Result.Failure<PropertyMutationReceiptDto>(
                actorResult.Error);
        }

        PropertyMutationActor actor = actorResult.Value;
        string fingerprint =
            PropertyLifecycleMutationFingerprint.ComputeSuspension(
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
            PropertyMutationKind.ProcessingSuspension,
            command.ExpectedVersion,
            fingerprint,
            cancellationToken).ConfigureAwait(false);
        if (replay.Exists)
        {
            return replay.ToResult();
        }

        PropertyGovernanceRevisionCoordinates? coordinates = ActivatePropertyProcessingCommandHandler.ToCoordinates(
            property.GovernanceBinding,
            property.GovernanceAcknowledgements);
        DateTimeOffset nowUtc = clock.UtcNow;
        Result suspension = property.SuspendProcessing(
            command.ExpectedVersion,
            idGenerator.NewId(),
            nowUtc,
            actor.Value!);
        if (suspension.IsFailure)
        {
            return Result.Failure<PropertyMutationReceiptDto>(
                suspension.Error);
        }

        await revisions.AppendAsync(
            new PropertyGovernanceRevisionWriteModel(
                idGenerator.NewId(),
                property.ScopeId,
                property.Id,
                property.Version,
                PropertyGovernanceRevisionAction.Suspended,
                "OperatorSuspended",
                coordinates,
                coordinates,
                actor.Value!,
                nowUtc),
            cancellationToken).ConfigureAwait(false);

        PropertyMutationReceiptDto receipt = await journal.RecordAsync(
            property,
            command.OperationId,
            PropertyMutationKind.ProcessingSuspension,
            command.ExpectedVersion,
            fingerprint,
            nowUtc,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(receipt);
    }
}
