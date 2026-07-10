namespace Inventory.Application.Handlers;

using Gma.Framework.Messaging;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Inventory.Application.Ports;
using Inventory.Contracts;
using Inventory.Domain.Aggregates;
using Inventory.Domain.Errors;

[IntegrationEventHandler(InventoryModuleMetadata.AllocationReleaseRequestedHandlerName)]
internal sealed class InventoryAllocationReleaseRequestedHandler(
    IInventoryAllocationRepository allocations,
    IOutboxWriterRegistry outboxWriters,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : IIntegrationEventHandler<InventoryAllocationReleaseRequestedIntegrationEvent>
{
    public async Task HandleAsync(
        InventoryAllocationReleaseRequestedIntegrationEvent request,
        CancellationToken cancellationToken)
    {
        InventoryAllocation? allocation = await allocations
            .GetAsync(request.AllocationId, cancellationToken)
            .ConfigureAwait(false);
        if (allocation is null)
        {
            await this.EnqueueRejectedAsync(request, InventoryAllocationReleaseRejectionReason.AllocationNotFound, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (allocation.ReservationId != request.ReservationId)
        {
            await this.EnqueueRejectedAsync(request, InventoryAllocationReleaseRejectionReason.ReservationMismatch, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        bool alreadyReleased = allocation.Status == InventoryAllocationState.Released;
        Result result = allocation.Release(request.ReleaseRequestId, request.ExpectedAllocationVersion, clock.UtcNow);
        if (result.IsFailure)
        {
            InventoryAllocationReleaseRejectionReason reason = result.Error == InventoryDomainErrors.VersionConflict
                ? InventoryAllocationReleaseRejectionReason.VersionConflict
                : InventoryAllocationReleaseRejectionReason.AllocationNotActive;
            await this.EnqueueRejectedAsync(request, reason, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!alreadyReleased)
        {
            await allocations.TouchUnitsAsync(
                allocation.Units.Select(unit => unit.InventoryUnitId).ToArray(),
                cancellationToken).ConfigureAwait(false);
        }

        await outboxWriters.GetRequired(InventoryModuleMetadata.Name).EnqueueAsync(
            new InventoryAllocationReleasedIntegrationEvent(
                idGenerator.NewId(),
                allocation.ScopeId,
                clock.UtcNow,
                allocation.Id,
                allocation.ReservationId,
                request.ReleaseRequestId,
                allocation.Version),
            cancellationToken).ConfigureAwait(false);
    }

    private Task EnqueueRejectedAsync(
        InventoryAllocationReleaseRequestedIntegrationEvent request,
        InventoryAllocationReleaseRejectionReason reason,
        CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(InventoryModuleMetadata.Name).EnqueueAsync(
            new InventoryAllocationReleaseRejectedIntegrationEvent(
                idGenerator.NewId(),
                request.ScopeId,
                clock.UtcNow,
                request.AllocationId,
                request.ReservationId,
                request.ReleaseRequestId,
                reason),
            cancellationToken);
}
