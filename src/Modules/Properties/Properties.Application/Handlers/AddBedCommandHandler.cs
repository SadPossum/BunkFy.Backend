namespace Properties.Application.Handlers;

using Properties.Application.Commands;
using Properties.Application.Mapping;
using Properties.Application.Ports;
using Properties.Contracts;
using Properties.Domain.Aggregates;
using Properties.Domain.Entities;
using Properties.Domain.Errors;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class AddBedCommandHandler(
    IRoomRepository repository,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<AddBedCommand, BedDto>
{
    public async Task<Result<BedDto>> HandleAsync(AddBedCommand command, CancellationToken cancellationToken)
    {
        Room? room = await repository.GetAsync(command.RoomId, cancellationToken).ConfigureAwait(false);
        if (room is null || room.PropertyId != command.PropertyId)
        {
            return Result.Failure<BedDto>(PropertiesDomainErrors.RoomNotFound);
        }

        Result<Bed> bedResult = room.AddBed(
            idGenerator.NewId(),
            command.Label,
            command.ExpectedRoomVersion,
            idGenerator.NewId(),
            clock.UtcNow);
        if (bedResult.IsFailure)
        {
            return Result.Failure<BedDto>(bedResult.Error);
        }

        return Result.Success(PropertiesMapper.ToDto(bedResult.Value, room.Version));
    }
}
