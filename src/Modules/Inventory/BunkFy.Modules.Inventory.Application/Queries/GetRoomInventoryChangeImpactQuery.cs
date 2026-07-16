namespace BunkFy.Modules.Inventory.Application.Queries;

using BunkFy.Modules.Inventory.Contracts;
using Gma.Framework.Cqrs;

public sealed record GetRoomInventoryChangeImpactQuery(Guid PropertyId, Guid RoomId)
    : IQuery<RoomInventoryChangeImpactDto>;
