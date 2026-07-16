namespace BunkFy.Modules.Inventory.Application.Commands;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Inventory.Contracts;

public sealed record ReleaseManualInventoryBlockCommand(
    Guid PropertyId,
    Guid BlockId,
    long ExpectedVersion,
    string? ActorId = null)
    : ITransactionalCommand<ManualInventoryBlockDto>;
