namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Messaging;

[IntegrationEventHandler(InventoryModuleMetadata.RoomRetirementFinalizedHandlerName)]
internal sealed class RoomRetirementFinalizedHandler(
    RoomRetirementOutcomeCoordinator outcomes)
    : IIntegrationEventHandler<RoomRetirementFinalizedIntegrationEvent>
{
    public Task HandleAsync(
        RoomRetirementFinalizedIntegrationEvent outcome,
        CancellationToken cancellationToken) => outcomes.MarkFinalizedAsync(
            outcome.PropertyId,
            outcome.TopologyChangeId,
            outcome.RoomId,
            cancellationToken);
}

[IntegrationEventHandler(InventoryModuleMetadata.RoomRetirementRejectedHandlerName)]
internal sealed class RoomRetirementFinalizationRejectedHandler(
    RoomRetirementOutcomeCoordinator outcomes)
    : IIntegrationEventHandler<RoomRetirementFinalizationRejectedIntegrationEvent>
{
    public Task HandleAsync(
        RoomRetirementFinalizationRejectedIntegrationEvent outcome,
        CancellationToken cancellationToken) => outcomes.RejectAsync(
            outcome.PropertyId,
            outcome.TopologyChangeId,
            outcome.RoomId,
            (int)outcome.Reason,
            cancellationToken);
}
