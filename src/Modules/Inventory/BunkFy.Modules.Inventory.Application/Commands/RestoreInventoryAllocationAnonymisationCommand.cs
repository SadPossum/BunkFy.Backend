namespace BunkFy.Modules.Inventory.Application.Commands;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Inventory.Domain.DataRights;
using Gma.Framework.Cqrs;

public sealed record RestoreInventoryAllocationAnonymisationCommand(
    DataRightsAnonymisationRestoreRequest Request)
    : ITransactionalCommand<
        InventoryAllocationAnonymisationRestoreReceipt>;
