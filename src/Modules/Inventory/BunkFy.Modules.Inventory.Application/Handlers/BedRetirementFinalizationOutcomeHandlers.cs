namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Messaging;

[IntegrationEventHandler(InventoryModuleMetadata.BedRetirementFinalizedHandlerName)]
internal sealed class BedRetirementFinalizedHandler(
    BedRetirementOutcomeCoordinator outcomes)
    : IIntegrationEventHandler<BedRetirementFinalizedIntegrationEvent>
{
    public Task HandleAsync(
        BedRetirementFinalizedIntegrationEvent outcome,
        CancellationToken cancellationToken) => outcomes.MarkFinalizedAsync(
            outcome.PropertyId,
            outcome.TopologyChangeId,
            outcome.RoomId,
            outcome.BedId,
            cancellationToken);
}

[IntegrationEventHandler(InventoryModuleMetadata.BedRetirementRejectedHandlerName)]
internal sealed class BedRetirementFinalizationRejectedHandler(
    BedRetirementOutcomeCoordinator outcomes)
    : IIntegrationEventHandler<BedRetirementFinalizationRejectedIntegrationEvent>
{
    public Task HandleAsync(
        BedRetirementFinalizationRejectedIntegrationEvent outcome,
        CancellationToken cancellationToken) => outcomes.RejectAsync(
            outcome.PropertyId,
            outcome.TopologyChangeId,
            outcome.RoomId,
            outcome.BedId,
            (int)outcome.Reason,
            cancellationToken);
}
