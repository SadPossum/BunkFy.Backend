namespace BunkFy.Modules.Reservations.Application.Handlers;

using Gma.Framework.Messaging;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;

[IntegrationEventHandler(ReservationsModuleMetadata.UnitDefinitionChangedHandlerName)]
internal sealed class InventoryUnitDefinitionChangedHandler(IInventoryProjectionRepository projection)
    : IIntegrationEventHandler<InventoryUnitDefinitionChangedIntegrationEvent>
{
    public Task HandleAsync(InventoryUnitDefinitionChangedIntegrationEvent e, CancellationToken cancellationToken) =>
        projection.ApplyUnitAsync(
            new(
                e.ScopeId,
                e.InventoryUnitId,
                e.PropertyId,
                e.RoomId,
                e.BedId,
                e.Kind,
                e.Label,
                e.IsTopologyActive,
                e.IsSellable,
                e.ConfigurationVersion,
                e.UnitVersion),
            cancellationToken);
}

[IntegrationEventHandler(ReservationsModuleMetadata.ManualBlockCreatedHandlerName)]
internal sealed class InventoryManualBlockCreatedHandler(IInventoryProjectionRepository projection)
    : IIntegrationEventHandler<ManualInventoryBlockCreatedIntegrationEvent>
{
    public Task HandleAsync(ManualInventoryBlockCreatedIntegrationEvent e, CancellationToken cancellationToken) =>
        projection.ApplyBlockAsync(
            new(
                e.ScopeId,
                e.BlockId,
                e.PropertyId,
                e.InventoryUnitId,
                e.Arrival,
                e.Departure,
                ManualInventoryBlockStatus.Active,
                e.BlockVersion),
            cancellationToken);
}

[IntegrationEventHandler(ReservationsModuleMetadata.ManualBlockReleasedHandlerName)]
internal sealed class InventoryManualBlockReleasedHandler(IInventoryProjectionRepository projection)
    : IIntegrationEventHandler<ManualInventoryBlockReleasedIntegrationEvent>
{
    public Task HandleAsync(ManualInventoryBlockReleasedIntegrationEvent e, CancellationToken cancellationToken) =>
        projection.ReleaseBlockAsync(
            e.ScopeId,
            e.PropertyId,
            e.InventoryUnitId,
            e.BlockId,
            e.BlockVersion,
            cancellationToken);
}
