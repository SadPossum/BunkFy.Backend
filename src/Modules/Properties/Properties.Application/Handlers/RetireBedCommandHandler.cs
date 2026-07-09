namespace Properties.Application.Handlers;

using Properties.Application.Commands;
using Properties.Application.Ports;
using Properties.Domain.Aggregates;
using Properties.Domain.Errors;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class RetireBedCommandHandler(
    IRoomRepository repository,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<RetireBedCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(RetireBedCommand command, CancellationToken cancellationToken)
    {
        Room? room = await repository.GetAsync(command.RoomId, cancellationToken).ConfigureAwait(false);
        if (room is null)
        {
            return Result.Failure<Unit>(PropertiesDomainErrors.RoomNotFound);
        }

        Result result = room.RetireBed(command.BedId, idGenerator.NewId(), clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<Unit>(result.Error);
        }

        return Result.Success(Unit.Value);
    }
}
