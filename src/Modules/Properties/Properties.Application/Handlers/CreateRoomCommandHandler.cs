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
using Gma.Framework.Scoping;

internal sealed class CreateRoomCommandHandler(
    IPropertyRepository propertyRepository,
    IRoomRepository roomRepository,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<CreateRoomCommand, RoomDto>
{
    public async Task<Result<RoomDto>> HandleAsync(CreateRoomCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<RoomDto>(PropertiesDomainErrors.TenantRequired);
        }

        Property? property = await propertyRepository.GetAsync(command.PropertyId, cancellationToken).ConfigureAwait(false);
        if (property is null)
        {
            return Result.Failure<RoomDto>(PropertiesDomainErrors.PropertyNotFound);
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
            return Result.Failure<RoomDto>(roomResult.Error);
        }

        Room room = roomResult.Value;
        if (await roomRepository.RoomNameExistsAsync(room.PropertyId, room.Name.Value, null, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<RoomDto>(PropertiesDomainErrors.RoomAlreadyExists);
        }

        Result registrationResult = property.RegisterRoom(command.ExpectedPropertyVersion);
        if (registrationResult.IsFailure)
        {
            return Result.Failure<RoomDto>(registrationResult.Error);
        }

        await roomRepository.AddAsync(room, cancellationToken).ConfigureAwait(false);

        return Result.Success(PropertiesMapper.ToDto(room));
    }
}
