namespace BunkFy.Modules.Inventory.Application.Commands;

using BunkFy.Modules.Inventory.Contracts;
using Gma.Framework.Cqrs;

public sealed record RetryRoomRetirementCommand(
    Guid OperationId,
    Guid PropertyId,
    Guid TopologyChangeId,
    long ExpectedVersion)
    : ITransactionalCommand<RoomRetirementDto>;
