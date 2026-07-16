namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Messaging;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

[IntegrationEventHandler(InventoryModuleMetadata.RoomRetirementFinalizedHandlerName)]
internal sealed class RoomRetirementFinalizedHandler(
    IRoomRetirementRepository retirements,
    ISystemClock clock)
    : IIntegrationEventHandler<RoomRetirementFinalizedIntegrationEvent>
{
    public async Task HandleAsync(RoomRetirementFinalizedIntegrationEvent outcome, CancellationToken cancellationToken)
    {
        RoomRetirementProcess? process = await retirements
            .GetAsync(outcome.PropertyId, outcome.TopologyChangeId, cancellationToken)
            .ConfigureAwait(false);
        if (process is null || process.RoomId != outcome.RoomId)
        {
            throw new InvalidOperationException("Room-retirement completion does not match a durable Inventory process.");
        }

        Result finalized = process.MarkFinalized(clock.UtcNow);
        if (finalized.IsFailure)
        {
            throw new InvalidOperationException(
                $"Room-retirement completion failed with '{finalized.Error.Code}'.");
        }
    }
}

[IntegrationEventHandler(InventoryModuleMetadata.RoomRetirementRejectedHandlerName)]
internal sealed class RoomRetirementFinalizationRejectedHandler(
    IRoomRetirementRepository retirements,
    ISystemClock clock)
    : IIntegrationEventHandler<RoomRetirementFinalizationRejectedIntegrationEvent>
{
    public async Task HandleAsync(
        RoomRetirementFinalizationRejectedIntegrationEvent outcome,
        CancellationToken cancellationToken)
    {
        RoomRetirementProcess? process = await retirements
            .GetAsync(outcome.PropertyId, outcome.TopologyChangeId, cancellationToken)
            .ConfigureAwait(false);
        if (process is null || process.RoomId != outcome.RoomId)
        {
            throw new InvalidOperationException("Room-retirement rejection does not match a durable Inventory process.");
        }

        Result rejected = process.Reject((int)outcome.Reason, clock.UtcNow);
        if (rejected.IsFailure)
        {
            throw new InvalidOperationException(
                $"Room-retirement rejection failed with '{rejected.Error.Code}'.");
        }
    }
}
