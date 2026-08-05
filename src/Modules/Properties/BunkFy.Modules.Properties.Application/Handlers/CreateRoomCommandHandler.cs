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
using Gma.Framework.Scoping;

internal sealed class CreateRoomCommandHandler(
    IPropertyRepository propertyRepository,
    IRoomRepository roomRepository,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<CreateRoomCommand, RoomMutationReceiptDto>
{
    public async Task<Result<RoomMutationReceiptDto>> HandleAsync(
        CreateRoomCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<RoomMutationReceiptDto>(PropertiesDomainErrors.TenantRequired);
        }

        Property? property = await propertyRepository.GetAsync(command.PropertyId, cancellationToken).ConfigureAwait(false);
        if (property is null)
        {
            return Result.Failure<RoomMutationReceiptDto>(PropertiesDomainErrors.PropertyNotFound);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result<Room> roomResult = Room.Create(
            idGenerator.NewId(),
            scopeContext.ScopeId,
            command.PropertyId,
            command.Name,
            command.BuildingLabel,
            command.FloorLabel,
            idGenerator.NewId(),
            nowUtc);

        if (roomResult.IsFailure)
        {
            return Result.Failure<RoomMutationReceiptDto>(roomResult.Error);
        }

        Room room = roomResult.Value;
        if (await roomRepository.RoomNameExistsAsync(room.PropertyId, room.Name.Value, null, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<RoomMutationReceiptDto>(PropertiesDomainErrors.RoomAlreadyExists);
        }

        Result registrationResult = property.RegisterRoom(command.ExpectedPropertyVersion);
        if (registrationResult.IsFailure)
        {
            return Result.Failure<RoomMutationReceiptDto>(registrationResult.Error);
        }

        await roomRepository.AddAsync(room, cancellationToken).ConfigureAwait(false);

        return Result.Success(PropertiesMapper.ToReceipt(room));
    }
}
