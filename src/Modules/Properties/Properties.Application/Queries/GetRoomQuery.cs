namespace Properties.Application.Queries;

using Properties.Contracts;
using Gma.Framework.Cqrs;

public sealed record GetRoomQuery(Guid PropertyId, Guid RoomId) : IQuery<RoomDto>;
