namespace BunkFy.Modules.Inventory.Application.Commands;

using BunkFy.Modules.Inventory.Contracts;
using Gma.Framework.Cqrs;

public sealed record ReleaseManualInventoryBlockGroupCommand(
    Guid OperationId,
    Guid PropertyId,
    Guid BlockGroupId,
    string? ActorId = null)
    : ITransactionalCommand<ManualInventoryBlockGroupMutationReceiptDto>;
