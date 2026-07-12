namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Application.Queries;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Errors;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class GetRoomQueryHandler(IPropertiesReadRepository repository)
    : IQueryHandler<GetRoomQuery, RoomDto>
{
    public async Task<Result<RoomDto>> HandleAsync(GetRoomQuery query, CancellationToken cancellationToken)
    {
        RoomDto? room = await repository.GetRoomAsync(query.PropertyId, query.RoomId, cancellationToken).ConfigureAwait(false);
        return room is null
            ? Result.Failure<RoomDto>(PropertiesDomainErrors.RoomNotFound)
            : Result.Success(room);
    }
}
