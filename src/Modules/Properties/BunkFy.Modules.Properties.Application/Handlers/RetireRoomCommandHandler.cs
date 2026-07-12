namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Entities;
using BunkFy.Modules.Properties.Domain.Errors;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class RetireRoomCommandHandler(
    IRoomRepository repository,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<RetireRoomCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(RetireRoomCommand command, CancellationToken cancellationToken)
    {
        Room? room = await repository.GetAsync(command.RoomId, cancellationToken).ConfigureAwait(false);
        if (room is null || room.PropertyId != command.PropertyId)
        {
            return Result.Failure<Unit>(PropertiesDomainErrors.RoomNotFound);
        }

        Guid[] bedEventIds = room.Beds
            .Where(bed => bed.Status == BedState.Active)
            .Select(_ => idGenerator.NewId())
            .ToArray();
        Result result = room.Retire(
            command.ExpectedVersion,
            command.CascadeBeds,
            bedEventIds,
            idGenerator.NewId(),
            clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<Unit>(result.Error);
        }

        return Result.Success(Unit.Value);
    }
}
