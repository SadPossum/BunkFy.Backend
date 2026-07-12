namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Application.Mapping;
using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Errors;
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
        if (room is null || room.PropertyId != command.PropertyId)
        {
            return Result.Failure<RoomDto>(PropertiesDomainErrors.RoomNotFound);
        }

        Result result = room.Update(
            command.Name,
            command.BuildingLabel,
            command.FloorLabel,
            command.ExpectedVersion,
            idGenerator.NewId(),
            clock.UtcNow);
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
