namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Application.Mapping;
using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Entities;
using BunkFy.Modules.Properties.Domain.Errors;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class AddBedCommandHandler(
    IRoomRepository repository,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<AddBedCommand, BedMutationReceiptDto>
{
    public async Task<Result<BedMutationReceiptDto>> HandleAsync(
        AddBedCommand command,
        CancellationToken cancellationToken)
    {
        Room? room = await repository.GetAsync(command.RoomId, cancellationToken).ConfigureAwait(false);
        if (room is null || room.PropertyId != command.PropertyId)
        {
            return Result.Failure<BedMutationReceiptDto>(PropertiesDomainErrors.RoomNotFound);
        }

        Result<Bed> bedResult = room.AddBed(
            idGenerator.NewId(),
            command.Label,
            command.ExpectedRoomVersion,
            idGenerator.NewId(),
            clock.UtcNow);
        if (bedResult.IsFailure)
        {
            return Result.Failure<BedMutationReceiptDto>(bedResult.Error);
        }

        return Result.Success(PropertiesMapper.ToReceipt(bedResult.Value, room.Version));
    }
}
