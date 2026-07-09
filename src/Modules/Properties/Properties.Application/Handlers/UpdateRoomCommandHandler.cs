namespace Properties.Application.Handlers;

using Properties.Application.Commands;
using Properties.Application.Mapping;
using Properties.Application.Ports;
using Properties.Contracts;
using Properties.Domain.Aggregates;
using Properties.Domain.Errors;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class UpdateRoomCommandHandler(
    IRoomRepository repository,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<UpdateRoomCommand, RoomDto>
{
    public async Task<Result<RoomDto>> HandleAsync(UpdateRoomCommand command, CancellationToken cancellationToken)
    {
        Room? room = await repository.GetAsync(command.RoomId, cancellationToken).ConfigureAwait(false);
        if (room is null)
        {
            return Result.Failure<RoomDto>(PropertiesDomainErrors.RoomNotFound);
        }

        Result result = room.Update(command.Name, command.BuildingLabel, command.FloorLabel, idGenerator.NewId(), clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<RoomDto>(result.Error);
        }

        if (await repository.RoomNameExistsAsync(room.PropertyId, room.Name.Value, room.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<RoomDto>(PropertiesDomainErrors.RoomAlreadyExists);
        }

        return Result.Success(PropertiesMapper.ToDto(room));
    }
}
