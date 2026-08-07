namespace BunkFy.Modules.Reservations.Application.Handlers;

using System.Text.Json;
using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Tasks;

[IntegrationEventHandler(ReservationsModuleMetadata.GuestRecordLinkReadyHandlerName)]
internal sealed class ReservationGuestRecordLinkReadyHandler(
    ITaskRunStore taskRuns,
    ISystemClock clock,
    IIdGenerator ids)
    : IIntegrationEventHandler<ReservationGuestRecordLinkReadyIntegrationEvent>
{
    public async Task HandleAsync(
        ReservationGuestRecordLinkReadyIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        DateTimeOffset nowUtc = clock.UtcNow;
        ExecuteReservationGuestRecordLinkPayload payload = new(
            integrationEvent.OperationId,
            integrationEvent.PropertyId,
            integrationEvent.ReservationId,
            integrationEvent.DispatchRevision);
        TaskRunRequest request = new(
            ids.NewId(),
            ReservationsModuleMetadata.Name,
            ExecuteReservationGuestRecordLinkPayload.TaskName,
            JsonSerializer.Serialize(payload),
            nowUtc,
            nowUtc,
            ReservationsModuleMetadata.GuestRecordLinkWorkerGroup,
            integrationEvent.ScopeId,
            integrationEvent.ReservationId,
            requestedBy: "system:reservations",
            ExecuteReservationGuestRecordLinkPayload.MaximumAttempts,
            ExecuteReservationGuestRecordLinkPayload.PayloadVersion,
            $"{integrationEvent.OperationId:N}:{integrationEvent.DispatchRevision}");
        _ = await taskRuns.EnqueueAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }
}
