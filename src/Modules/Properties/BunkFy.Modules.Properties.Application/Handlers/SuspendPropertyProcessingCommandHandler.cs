namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Errors;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class SuspendPropertyProcessingCommandHandler(
    IPropertyRepository properties,
    IPropertyGovernanceRevisionWriter revisions,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<SuspendPropertyProcessingCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        SuspendPropertyProcessingCommand command,
        CancellationToken cancellationToken)
    {
        Property? property = await properties.GetAsync(command.PropertyId, cancellationToken).ConfigureAwait(false);
        if (property is null)
        {
            return Result.Failure<Unit>(PropertiesDomainErrors.PropertyNotFound);
        }

        PropertyGovernanceRevisionCoordinates? coordinates = ActivatePropertyProcessingCommandHandler.ToCoordinates(
            property.GovernanceBinding,
            property.GovernanceAcknowledgements);
        DateTimeOffset nowUtc = clock.UtcNow;
        Result suspension = property.SuspendProcessing(
            command.ExpectedVersion,
            idGenerator.NewId(),
            nowUtc,
            command.ActorId);
        if (suspension.IsFailure)
        {
            return Result.Failure<Unit>(suspension.Error);
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
                command.ActorId.Trim(),
                nowUtc),
            cancellationToken).ConfigureAwait(false);

        return Result.Success(Unit.Value);
    }
}
