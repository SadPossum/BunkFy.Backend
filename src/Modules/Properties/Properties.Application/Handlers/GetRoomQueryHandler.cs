namespace Properties.Application.Handlers;

using Properties.Application.Ports;
using Properties.Application.Queries;
using Properties.Contracts;
using Properties.Domain.Errors;
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
