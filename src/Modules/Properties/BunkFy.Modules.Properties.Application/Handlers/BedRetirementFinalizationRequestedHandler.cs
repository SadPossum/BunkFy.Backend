namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Entities;
using BunkFy.Modules.Properties.Domain.Errors;
using Gma.Framework.Messaging;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

[IntegrationEventHandler(PropertiesModuleMetadata.BedRetirementFinalizationHandlerName)]
internal sealed class BedRetirementFinalizationRequestedHandler(
    IRoomRepository rooms,
    IOutboxWriterRegistry outboxWriters,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : IIntegrationEventHandler<BedRetirementFinalizationRequestedIntegrationEvent>
{
    public async Task HandleAsync(
        BedRetirementFinalizationRequestedIntegrationEvent request,
        CancellationToken cancellationToken)
    {
        Room? room = await rooms.GetAsync(request.RoomId, cancellationToken).ConfigureAwait(false);
        if (room is null || room.PropertyId != request.PropertyId)
        {
            await this.RejectAsync(
                request,
                BedRetirementFinalizationRejectionReason.RoomNotFound,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        Bed? bed = room.Beds.SingleOrDefault(candidate => candidate.Id == request.BedId);
        if (bed is null)
        {
            await this.RejectAsync(
                request,
                BedRetirementFinalizationRejectionReason.BedNotFound,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (bed.Status == BedState.Active)
        {
            if (room.Status != RoomState.Active)
            {
                await this.RejectAsync(
                    request,
                    BedRetirementFinalizationRejectionReason.RoomRetired,
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            Result retired = room.RetireBed(
                request.BedId,
                room.Version,
                idGenerator.NewId(),
                clock.UtcNow);
            if (retired.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Validated bed-retirement finalization failed with '{retired.Error.Code}'.");
            }
        }

        await outboxWriters.GetRequired(PropertiesModuleMetadata.Name).EnqueueAsync(
            new BedRetirementFinalizedIntegrationEvent(
                idGenerator.NewId(),
                request.ScopeId,
                clock.UtcNow,
                request.TopologyChangeId,
                request.PropertyId,
                request.RoomId,
                request.BedId,
                room.Version,
                bed.Version),
            cancellationToken).ConfigureAwait(false);
    }

    private Task RejectAsync(
        BedRetirementFinalizationRequestedIntegrationEvent request,
        BedRetirementFinalizationRejectionReason reason,
        CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(PropertiesModuleMetadata.Name).EnqueueAsync(
            new BedRetirementFinalizationRejectedIntegrationEvent(
                idGenerator.NewId(),
                request.ScopeId,
                clock.UtcNow,
                request.TopologyChangeId,
                request.PropertyId,
                request.RoomId,
                request.BedId,
                reason),
            cancellationToken);
}
