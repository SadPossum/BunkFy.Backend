namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Entities;
using Gma.Framework.Messaging;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

[IntegrationEventHandler(PropertiesModuleMetadata.RoomRetirementFinalizationHandlerName)]
internal sealed class RoomRetirementFinalizationRequestedHandler(
    IRoomRepository rooms,
    IOutboxWriterRegistry outboxWriters,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : IIntegrationEventHandler<RoomRetirementFinalizationRequestedIntegrationEvent>
{
    public async Task HandleAsync(
        RoomRetirementFinalizationRequestedIntegrationEvent request,
        CancellationToken cancellationToken)
    {
        Room? room = await rooms.GetAsync(request.RoomId, cancellationToken).ConfigureAwait(false);
        if (room is null || room.PropertyId != request.PropertyId)
        {
            await outboxWriters.GetRequired(PropertiesModuleMetadata.Name).EnqueueAsync(
                new RoomRetirementFinalizationRejectedIntegrationEvent(
                    idGenerator.NewId(),
                    request.ScopeId,
                    clock.UtcNow,
                    request.TopologyChangeId,
                    request.PropertyId,
                    request.RoomId,
                    RoomRetirementFinalizationRejectionReason.RoomNotFound),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (room.Status == RoomState.Active)
        {
            Guid[] bedEventIds = room.Beds
                .Where(bed => bed.Status == BedState.Active)
                .Select(_ => idGenerator.NewId())
                .ToArray();
            Result retired = room.Retire(
                room.Version,
                cascadeBeds: true,
                bedEventIds,
                idGenerator.NewId(),
                clock.UtcNow);
            if (retired.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Validated room-retirement finalization failed with '{retired.Error.Code}'.");
            }
        }

        await outboxWriters.GetRequired(PropertiesModuleMetadata.Name).EnqueueAsync(
            new RoomRetirementFinalizedIntegrationEvent(
                idGenerator.NewId(),
                request.ScopeId,
                clock.UtcNow,
                request.TopologyChangeId,
                request.PropertyId,
                request.RoomId,
                room.Version),
            cancellationToken).ConfigureAwait(false);
    }
}
