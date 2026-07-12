namespace BunkFy.Modules.Properties.Application.Queries;

using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Cqrs;

public sealed record GetRoomQuery(Guid PropertyId, Guid RoomId) : IQuery<RoomDto>;
