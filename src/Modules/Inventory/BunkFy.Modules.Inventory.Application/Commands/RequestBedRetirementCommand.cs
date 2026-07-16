namespace BunkFy.Modules.Inventory.Application.Commands;

using BunkFy.Modules.Inventory.Contracts;
using Gma.Framework.Cqrs;

public sealed record RequestBedRetirementCommand(
    Guid PropertyId,
    Guid RoomId,
    Guid BedId,
    string Reason,
    string RequestedBy)
    : ITransactionalCommand<BedRetirementDto>;
