namespace Inventory.Application.Handlers;

using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Inventory.Application.Ports;
using Inventory.Contracts;
using Inventory.Domain.Aggregates;

[IntegrationEventHandler(InventoryModuleMetadata.AllocationRequestedHandlerName)]
internal sealed class InventoryAllocationRequestedHandler(
    IInventoryAllocationRepository allocations,
    IOutboxWriterRegistry outboxWriters,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : IIntegrationEventHandler<InventoryAllocationRequestedIntegrationEvent>
{
    public async Task HandleAsync(
        InventoryAllocationRequestedIntegrationEvent request,
        CancellationToken cancellationToken)
    {
        InventoryAllocation? existing = await allocations
            .GetByRequestAsync(request.AllocationRequestId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            if (!existing.MatchesRequest(
                    request.ReservationId,
                    request.PropertyId,
                    request.Arrival,
                    request.Departure,
                    request.InventoryUnitIds))
            {
                await this.EnqueueRejectedAsync(request, InventoryAllocationRejectionReason.RequestMismatch, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            await this.EnqueueExistingDecisionAsync(existing, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (await allocations.GetByReservationAsync(request.ReservationId, cancellationToken).ConfigureAwait(false) is not null)
        {
            await this.EnqueueRejectedAsync(
                request,
                InventoryAllocationRejectionReason.ExistingActiveAllocation,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        InventoryAllocationRejection rejection = await this
            .EvaluateAsync(request, cancellationToken)
            .ConfigureAwait(false);
        InventoryAllocation decision = rejection == InventoryAllocationRejection.None
            ? InventoryAllocation.CreateAccepted(
                idGenerator.NewId(),
                request.ScopeId,
                request.ReservationId,
                request.AllocationRequestId,
                request.PropertyId,
                request.Arrival,
                request.Departure,
                request.InventoryUnitIds,
                clock.UtcNow).Value
            : InventoryAllocation.CreateRejected(
                idGenerator.NewId(),
                request.ScopeId,
                request.ReservationId,
                request.AllocationRequestId,
                request.PropertyId,
                request.Arrival,
                request.Departure,
                request.InventoryUnitIds,
                rejection,
                clock.UtcNow).Value;

        if (decision.Status == InventoryAllocationState.Active)
        {
            await allocations.TouchUnitsAsync(request.InventoryUnitIds, cancellationToken).ConfigureAwait(false);
        }

        await allocations.AddAsync(decision, cancellationToken).ConfigureAwait(false);
        await this.EnqueueExistingDecisionAsync(decision, cancellationToken).ConfigureAwait(false);
    }

    private async Task<InventoryAllocationRejection> EvaluateAsync(
        InventoryAllocationRequestedIntegrationEvent request,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<InventoryAllocationUnitSnapshot> units = await allocations
            .GetUnitsAsync(request.PropertyId, request.InventoryUnitIds, cancellationToken)
            .ConfigureAwait(false);
        if (units.Count != request.InventoryUnitIds.Count)
        {
            return InventoryAllocationRejection.UnitNotFound;
        }

        if (units.Any(unit => !unit.IsTopologyActive))
        {
            return InventoryAllocationRejection.UnitInactive;
        }

        if (units.Any(unit => !unit.IsSellable))
        {
            return InventoryAllocationRejection.UnitNotSellable;
        }

        if (await allocations.HasManualBlockConflictAsync(
                request.InventoryUnitIds,
                request.Arrival,
                request.Departure,
                cancellationToken).ConfigureAwait(false))
        {
            return InventoryAllocationRejection.ManualBlockConflict;
        }

        return await allocations.HasActiveAllocationConflictAsync(
                request.InventoryUnitIds,
                request.Arrival,
                request.Departure,
                cancellationToken).ConfigureAwait(false)
            ? InventoryAllocationRejection.AllocationConflict
            : InventoryAllocationRejection.None;
    }

    private Task EnqueueExistingDecisionAsync(
        InventoryAllocation allocation,
        CancellationToken cancellationToken)
    {
        if (allocation.Status is InventoryAllocationState.Active or InventoryAllocationState.Released)
        {
            return outboxWriters.GetRequired(InventoryModuleMetadata.Name).EnqueueAsync(
                new InventoryAllocationConfirmedIntegrationEvent(
                    idGenerator.NewId(),
                    allocation.ScopeId,
                    clock.UtcNow,
                    allocation.Id,
                    allocation.ReservationId,
                    allocation.AllocationRequestId,
                    allocation.PropertyId,
                    allocation.Arrival,
                    allocation.Departure,
                    allocation.Units.Select(unit => unit.InventoryUnitId).ToArray(),
                    1),
                cancellationToken);
        }

        return outboxWriters.GetRequired(InventoryModuleMetadata.Name).EnqueueAsync(
            new InventoryAllocationRejectedIntegrationEvent(
                idGenerator.NewId(),
                allocation.ScopeId,
                clock.UtcNow,
                allocation.ReservationId,
                allocation.AllocationRequestId,
                allocation.PropertyId,
                Map(allocation.Rejection)),
            cancellationToken);
    }

    private Task EnqueueRejectedAsync(
        InventoryAllocationRequestedIntegrationEvent request,
        InventoryAllocationRejectionReason reason,
        CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(InventoryModuleMetadata.Name).EnqueueAsync(
            new InventoryAllocationRejectedIntegrationEvent(
                idGenerator.NewId(),
                request.ScopeId,
                clock.UtcNow,
                request.ReservationId,
                request.AllocationRequestId,
                request.PropertyId,
                reason),
            cancellationToken);

    private static InventoryAllocationRejectionReason Map(InventoryAllocationRejection rejection) => rejection switch
    {
        InventoryAllocationRejection.UnitNotFound => InventoryAllocationRejectionReason.UnitNotFound,
        InventoryAllocationRejection.UnitInactive => InventoryAllocationRejectionReason.UnitInactive,
        InventoryAllocationRejection.UnitNotSellable => InventoryAllocationRejectionReason.UnitNotSellable,
        InventoryAllocationRejection.ManualBlockConflict => InventoryAllocationRejectionReason.ManualBlockConflict,
        InventoryAllocationRejection.AllocationConflict => InventoryAllocationRejectionReason.AllocationConflict,
        InventoryAllocationRejection.ExistingActiveAllocation => InventoryAllocationRejectionReason.ExistingActiveAllocation,
        _ => throw new InvalidOperationException("Rejected allocation has no supported reason.")
    };
}
