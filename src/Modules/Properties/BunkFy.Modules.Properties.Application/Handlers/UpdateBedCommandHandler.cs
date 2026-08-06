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

internal sealed class UpdateBedCommandHandler(
    PropertiesMutationCoordinator mutations,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<UpdateBedCommand, BedMutationReceiptDto>
{
    public async Task<Result<BedMutationReceiptDto>> HandleAsync(
        UpdateBedCommand command,
        CancellationToken cancellationToken)
    {
        Room? room = await mutations
            .AcquireRoomAsync(
                command.RoomId,
                cancellationToken).ConfigureAwait(false);
        if (room is null || room.PropertyId != command.PropertyId)
        {
            return Result.Failure<BedMutationReceiptDto>(PropertiesDomainErrors.RoomNotFound);
        }

        Result<Bed> bedResult = room.UpdateBed(
            command.BedId,
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
