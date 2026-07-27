namespace BunkFy.Modules.Inventory.Application.Commands;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Inventory.Contracts;
using Gma.Framework.Cqrs;

public sealed record ApplyInventoryAllocationAnonymisationCommand(
    DataRightsAnonymisationContributionRequest Request)
    : ITransactionalCommand<
        InventoryAllocationAnonymisationReceiptDto>;
