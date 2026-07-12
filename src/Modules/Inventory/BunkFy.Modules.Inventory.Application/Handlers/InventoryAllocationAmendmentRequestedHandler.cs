namespace BunkFy.Modules.Inventory.Application.Handlers;

using System.Security.Cryptography;
using System.Text;
using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;

[IntegrationEventHandler(InventoryModuleMetadata.AllocationAmendmentRequestedHandlerName)]
internal sealed class InventoryAllocationAmendmentRequestedHandler(
    IInventoryAllocationRepository allocations,
    IInventoryAllocationAmendmentDecisionRepository decisions,
    IOutboxWriterRegistry outboxWriters,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : IIntegrationEventHandler<InventoryAllocationAmendmentRequestedIntegrationEvent>
{
    public async Task HandleAsync(
        InventoryAllocationAmendmentRequestedIntegrationEvent request,
        CancellationToken cancellationToken)
    {
        string fingerprint = Fingerprint(request);
        InventoryAllocationAmendmentDecisionRecord? existing = await decisions
            .GetAsync(request.AmendmentRequestId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            if (string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal))
            {
                await this.PublishAsync(request, existing, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await this.PublishRejectedAsync(
                    request,
                    InventoryAllocationRejectionReason.RequestMismatch,
                    cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        InventoryAllocation? allocation = await allocations.GetAsync(request.AllocationId, cancellationToken)
            .ConfigureAwait(false);
        InventoryAllocationRejectionReason? identityRejection = allocation switch
        {
            null => InventoryAllocationRejectionReason.AllocationNotFound,
            { ReservationId: var reservationId } when reservationId != request.ReservationId =>
                InventoryAllocationRejectionReason.ReservationMismatch,
            { PropertyId: var propertyId } when propertyId != request.PropertyId =>
                InventoryAllocationRejectionReason.RequestMismatch,
            { Status: not InventoryAllocationState.Active } => InventoryAllocationRejectionReason.AllocationNotActive,
            { Version: var version } when version != request.ExpectedAllocationVersion =>
                InventoryAllocationRejectionReason.VersionConflict,
            _ => null
        };
        if (identityRejection.HasValue)
        {
            await this.RejectAsync(request, fingerprint, identityRejection.Value, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        InventoryAllocationRejectionReason? availabilityRejection = await this.EvaluateAsync(
            allocation!,
            request,
            cancellationToken).ConfigureAwait(false);
        if (availabilityRejection.HasValue)
        {
            await this.RejectAsync(request, fingerprint, availabilityRejection.Value, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        Guid[] touchedUnits = allocation!.Units.Select(unit => unit.InventoryUnitId)
            .Concat(request.InventoryUnitIds)
            .Distinct()
            .ToArray();
        await allocations.TouchUnitsAsync(touchedUnits, cancellationToken).ConfigureAwait(false);
        var amended = allocation.Amend(
            request.AmendmentRequestId,
            request.ExpectedAllocationVersion,
            request.Arrival,
            request.Departure,
            request.InventoryUnitIds);
        if (amended.IsFailure)
        {
            throw new InvalidOperationException(
                $"Validated allocation amendment failed with '{amended.Error.Code}'.");
        }

        InventoryAllocationAmendmentDecisionRecord confirmed = new(
            request.AmendmentRequestId,
            request.ScopeId,
            request.AllocationId,
            request.ReservationId,
            request.PropertyId,
            fingerprint,
            Confirmed: true,
            RejectionReason: null,
            allocation.Version,
            clock.UtcNow);
        await decisions.AddAsync(confirmed, cancellationToken).ConfigureAwait(false);
        await this.PublishAsync(request, confirmed, cancellationToken).ConfigureAwait(false);
    }

    private async Task<InventoryAllocationRejectionReason?> EvaluateAsync(
        InventoryAllocation allocation,
        InventoryAllocationAmendmentRequestedIntegrationEvent request,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<InventoryAllocationUnitSnapshot> units = await allocations
            .GetUnitsAsync(request.PropertyId, request.InventoryUnitIds, cancellationToken)
            .ConfigureAwait(false);
        if (units.Count != request.InventoryUnitIds.Count)
        {
            return InventoryAllocationRejectionReason.UnitNotFound;
        }

        if (units.Any(unit => !unit.IsTopologyActive))
        {
            return InventoryAllocationRejectionReason.UnitInactive;
        }

        if (units.Any(unit => !unit.IsSellable))
        {
            return InventoryAllocationRejectionReason.UnitNotSellable;
        }

        if (await allocations.HasManualBlockConflictAsync(
                request.InventoryUnitIds,
                request.Arrival,
                request.Departure,
                cancellationToken).ConfigureAwait(false))
        {
            return InventoryAllocationRejectionReason.ManualBlockConflict;
        }

        return await allocations.HasActiveAllocationConflictAsync(
                request.InventoryUnitIds,
                request.Arrival,
                request.Departure,
                allocation.Id,
                cancellationToken).ConfigureAwait(false)
            ? InventoryAllocationRejectionReason.AllocationConflict
            : null;
    }

    private async Task RejectAsync(
        InventoryAllocationAmendmentRequestedIntegrationEvent request,
        string fingerprint,
        InventoryAllocationRejectionReason reason,
        CancellationToken cancellationToken)
    {
        InventoryAllocationAmendmentDecisionRecord rejected = new(
            request.AmendmentRequestId,
            request.ScopeId,
            request.AllocationId,
            request.ReservationId,
            request.PropertyId,
            fingerprint,
            Confirmed: false,
            reason,
            AllocationVersion: null,
            clock.UtcNow);
        await decisions.AddAsync(rejected, cancellationToken).ConfigureAwait(false);
        await this.PublishAsync(request, rejected, cancellationToken).ConfigureAwait(false);
    }

    private Task PublishAsync(
        InventoryAllocationAmendmentRequestedIntegrationEvent request,
        InventoryAllocationAmendmentDecisionRecord decision,
        CancellationToken cancellationToken) => decision.Confirmed
        ? outboxWriters.GetRequired(InventoryModuleMetadata.Name).EnqueueAsync(
            new InventoryAllocationAmendmentConfirmedIntegrationEvent(
                idGenerator.NewId(),
                request.ScopeId,
                clock.UtcNow,
                request.AmendmentRequestId,
                request.AllocationId,
                request.ReservationId,
                request.PropertyId,
                request.Arrival,
                request.Departure,
                request.InventoryUnitIds,
                decision.AllocationVersion!.Value),
            cancellationToken)
        : this.PublishRejectedAsync(request, decision.RejectionReason!.Value, cancellationToken);

    private Task PublishRejectedAsync(
        InventoryAllocationAmendmentRequestedIntegrationEvent request,
        InventoryAllocationRejectionReason reason,
        CancellationToken cancellationToken) => outboxWriters.GetRequired(InventoryModuleMetadata.Name).EnqueueAsync(
        new InventoryAllocationAmendmentRejectedIntegrationEvent(
            idGenerator.NewId(),
            request.ScopeId,
            clock.UtcNow,
            request.AmendmentRequestId,
            request.AllocationId,
            request.ReservationId,
            request.PropertyId,
            reason),
        cancellationToken);

    private static string Fingerprint(InventoryAllocationAmendmentRequestedIntegrationEvent request)
    {
        string canonical = string.Join(
            '|',
            request.AllocationId.ToString("N"),
            request.ReservationId.ToString("N"),
            request.PropertyId.ToString("N"),
            request.ExpectedAllocationVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            request.Arrival.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            request.Departure.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            string.Join(',', request.InventoryUnitIds.Order().Select(id => id.ToString("N"))));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
