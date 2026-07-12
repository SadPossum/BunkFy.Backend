namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Errors;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class RetirePropertyCommandHandler(
    IPropertyRepository propertyRepository,
    IRoomRepository roomRepository,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<RetirePropertyCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(RetirePropertyCommand command, CancellationToken cancellationToken)
    {
        Property? property = await propertyRepository.GetAsync(command.PropertyId, cancellationToken).ConfigureAwait(false);
        if (property is null)
        {
            return Result.Failure<Unit>(PropertiesDomainErrors.PropertyNotFound);
        }

        if (await roomRepository.HasActiveRoomsAsync(property.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<Unit>(PropertiesDomainErrors.PropertyHasActiveRooms);
        }

        Result result = property.Retire(command.ExpectedVersion, idGenerator.NewId(), clock.UtcNow);
        return result.IsSuccess
            ? Result.Success(Unit.Value)
            : Result.Failure<Unit>(result.Error);
    }
}
