namespace Inventory.Application.Commands;

using Gma.Framework.Cqrs;
using Inventory.Contracts;

public sealed record ReleaseManualInventoryBlockCommand(
    Guid PropertyId,
    Guid BlockId,
    long ExpectedVersion)
    : ITransactionalCommand<ManualInventoryBlockDto>;
