namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Messaging;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

[IntegrationEventHandler(InventoryModuleMetadata.BedRetirementFinalizedHandlerName)]
internal sealed class BedRetirementFinalizedHandler(
    IBedRetirementRepository retirements,
    ISystemClock clock)
    : IIntegrationEventHandler<BedRetirementFinalizedIntegrationEvent>
{
    public async Task HandleAsync(BedRetirementFinalizedIntegrationEvent outcome, CancellationToken cancellationToken)
    {
        BedRetirementProcess? process = await retirements
            .GetAsync(outcome.PropertyId, outcome.TopologyChangeId, cancellationToken)
            .ConfigureAwait(false);
        if (process is null || process.RoomId != outcome.RoomId || process.BedId != outcome.BedId)
        {
            throw new InvalidOperationException("Bed-retirement completion does not match a durable Inventory process.");
        }

        Result finalized = process.MarkFinalized(clock.UtcNow);
        if (finalized.IsFailure)
        {
            throw new InvalidOperationException(
                $"Bed-retirement completion failed with '{finalized.Error.Code}'.");
        }
    }
}

[IntegrationEventHandler(InventoryModuleMetadata.BedRetirementRejectedHandlerName)]
internal sealed class BedRetirementFinalizationRejectedHandler(
    IBedRetirementRepository retirements,
    ISystemClock clock)
    : IIntegrationEventHandler<BedRetirementFinalizationRejectedIntegrationEvent>
{
    public async Task HandleAsync(
        BedRetirementFinalizationRejectedIntegrationEvent outcome,
        CancellationToken cancellationToken)
    {
        BedRetirementProcess? process = await retirements
            .GetAsync(outcome.PropertyId, outcome.TopologyChangeId, cancellationToken)
            .ConfigureAwait(false);
        if (process is null || process.RoomId != outcome.RoomId || process.BedId != outcome.BedId)
        {
            throw new InvalidOperationException("Bed-retirement rejection does not match a durable Inventory process.");
        }

        Result rejected = process.Reject((int)outcome.Reason, clock.UtcNow);
        if (rejected.IsFailure)
        {
            throw new InvalidOperationException(
                $"Bed-retirement rejection failed with '{rejected.Error.Code}'.");
        }
    }
}
