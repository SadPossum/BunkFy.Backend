namespace BunkFy.Modules.Inventory.Application.Commands;

using BunkFy.Modules.Inventory.Contracts;
using Gma.Framework.Cqrs;

public sealed record RequestBedRetirementCommand(
    Guid OperationId,
    Guid PropertyId,
    Guid RoomId,
    Guid BedId,
    bool Confirmed,
    string Reason,
    string RequestedBy)
    : ITransactionalCommand<BedRetirementDto>;
