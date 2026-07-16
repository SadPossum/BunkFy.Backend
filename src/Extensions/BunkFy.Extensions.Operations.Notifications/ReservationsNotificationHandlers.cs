namespace BunkFy.Extensions.Operations.Notifications;

using System.Text.Json;
using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Messaging;
using Gma.Modules.Notifications.Contracts;

[IntegrationEventHandler("bunkfy-reservation-confirmed-notification", RequiresExplicitProducerBinding = true)]
internal sealed class ReservationConfirmedNotificationHandler(OperationalNotificationProjector projector)
    : IIntegrationEventHandler<ReservationConfirmedIntegrationEvent>
{
    public Task HandleAsync(
        ReservationConfirmedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        projector.ProjectForPropertyAsync(
            integrationEvent.EventId,
            integrationEvent.TenantId,
            integrationEvent.OccurredAtUtc,
            integrationEvent.PropertyId,
            Reservation(
                integrationEvent,
                "reservation-confirmed",
                "Reservation confirmed",
                "A reservation was confirmed and inventory was allocated.",
                NotificationSeverity.Success),
            cancellationToken);

    private static OperationalNotification Reservation(
        ReservationConfirmedIntegrationEvent integrationEvent,
        string name,
        string title,
        string body,
        NotificationSeverity severity) =>
        new(
            ReservationsModuleMetadata.Name,
            name,
            title,
            body,
            severity,
            JsonSerializer.Serialize(new
            {
                integrationEvent.ReservationId,
                integrationEvent.PropertyId,
                integrationEvent.AllocationId,
                integrationEvent.ReservationVersion,
            }),
            BunkFyNotificationTags.ReservationActivity,
            integrationEvent.ActorId);
}

[IntegrationEventHandler("bunkfy-reservation-arrival-reminder-notification", RequiresExplicitProducerBinding = true)]
internal sealed class ReservationArrivalReminderNotificationHandler(OperationalNotificationProjector projector)
    : IIntegrationEventHandler<ReservationArrivalReminderDueIntegrationEvent>
{
    public Task HandleAsync(
        ReservationArrivalReminderDueIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        projector.ProjectForPropertyAsync(
            integrationEvent.EventId,
            integrationEvent.TenantId,
            integrationEvent.OccurredAtUtc,
            integrationEvent.PropertyId,
            new OperationalNotification(
                ReservationsModuleMetadata.Name,
                "reservation-arrival-soon",
                "Expected arrival soon",
                $"{integrationEvent.PrimaryGuestName} is expected at " +
                $"{integrationEvent.ExpectedArrivalTime:HH\\:mm} on {integrationEvent.Arrival:MMM d}.",
                NotificationSeverity.Info,
                JsonSerializer.Serialize(new
                {
                    integrationEvent.ReservationId,
                    integrationEvent.PropertyId,
                    integrationEvent.Arrival,
                    integrationEvent.ExpectedArrivalTime,
                    integrationEvent.TimeZoneId,
                    integrationEvent.DetailsRevision,
                }),
                BunkFyNotificationTags.ReservationActivity),
            cancellationToken);
}

[IntegrationEventHandler("bunkfy-reservation-allocation-rejected-notification", RequiresExplicitProducerBinding = true)]
internal sealed class ReservationAllocationRejectedNotificationHandler(OperationalNotificationProjector projector)
    : IIntegrationEventHandler<ReservationAllocationRejectedIntegrationEvent>
{
    public Task HandleAsync(
        ReservationAllocationRejectedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        projector.ProjectForPropertyAsync(
            integrationEvent.EventId,
            integrationEvent.TenantId,
            integrationEvent.OccurredAtUtc,
            integrationEvent.PropertyId,
            new OperationalNotification(
                ReservationsModuleMetadata.Name,
                "reservation-allocation-rejected",
                "Reservation needs attention",
                $"Inventory allocation was rejected: {integrationEvent.Reason}.",
                NotificationSeverity.Error,
                JsonSerializer.Serialize(new
                {
                    integrationEvent.ReservationId,
                    integrationEvent.PropertyId,
                    integrationEvent.Reason,
                    integrationEvent.ReservationVersion,
                }),
                BunkFyNotificationTags.ReservationActivity,
                integrationEvent.ActorId),
            cancellationToken);
}

[IntegrationEventHandler("bunkfy-reservation-cancelled-notification", RequiresExplicitProducerBinding = true)]
internal sealed class ReservationCancelledNotificationHandler(OperationalNotificationProjector projector)
    : IIntegrationEventHandler<ReservationCancelledIntegrationEvent>
{
    public Task HandleAsync(
        ReservationCancelledIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        projector.ProjectForPropertyAsync(
            integrationEvent.EventId,
            integrationEvent.TenantId,
            integrationEvent.OccurredAtUtc,
            integrationEvent.PropertyId,
            new OperationalNotification(
                ReservationsModuleMetadata.Name,
                "reservation-cancelled",
                "Reservation cancelled",
                "A reservation was cancelled and its inventory can be released.",
                NotificationSeverity.Warning,
                JsonSerializer.Serialize(new
                {
                    integrationEvent.ReservationId,
                    integrationEvent.PropertyId,
                    integrationEvent.ReservationVersion,
                }),
                BunkFyNotificationTags.ReservationActivity,
                integrationEvent.ActorId),
            cancellationToken);
}

[IntegrationEventHandler("bunkfy-reservation-no-show-notification", RequiresExplicitProducerBinding = true)]
internal sealed class ReservationNoShowNotificationHandler(OperationalNotificationProjector projector)
    : IIntegrationEventHandler<ReservationNoShowIntegrationEvent>
{
    public Task HandleAsync(
        ReservationNoShowIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        projector.ProjectForPropertyAsync(
            integrationEvent.EventId,
            integrationEvent.TenantId,
            integrationEvent.OccurredAtUtc,
            integrationEvent.PropertyId,
            new OperationalNotification(
                ReservationsModuleMetadata.Name,
                "reservation-no-show",
                "Reservation marked no-show",
                $"A reservation was marked no-show for {integrationEvent.BusinessDate:yyyy-MM-dd}.",
                NotificationSeverity.Warning,
                JsonSerializer.Serialize(new
                {
                    integrationEvent.ReservationId,
                    integrationEvent.PropertyId,
                    integrationEvent.BusinessDate,
                    integrationEvent.ActorId,
                    integrationEvent.ReservationVersion,
                }),
                BunkFyNotificationTags.ReservationActivity,
                integrationEvent.ActorId),
            cancellationToken);
}

[IntegrationEventHandler("bunkfy-provider-reservation-conflict-notification", RequiresExplicitProducerBinding = true)]
internal sealed class ExternalReservationOperationAttentionNotificationHandler(
    OperationalNotificationProjector projector)
    : IIntegrationEventHandler<ExternalReservationOperationCompletedIntegrationEvent>
{
    public Task HandleAsync(
        ExternalReservationOperationCompletedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        if (integrationEvent.Outcome is ExternalReservationOperationOutcome.Applied or
            ExternalReservationOperationOutcome.Accepted or
            ExternalReservationOperationOutcome.Unchanged)
        {
            return Task.CompletedTask;
        }

        return projector.ProjectForPropertyAsync(
            integrationEvent.EventId,
            integrationEvent.TenantId,
            integrationEvent.OccurredAtUtc,
            integrationEvent.PropertyId,
            new OperationalNotification(
                ReservationsModuleMetadata.Name,
                "provider-reservation-operation-needs-attention",
                "Provider update needs attention",
                $"A provider reservation {integrationEvent.OperationKind} operation ended as {integrationEvent.Outcome}.",
                NotificationSeverity.Error,
                JsonSerializer.Serialize(new
                {
                    integrationEvent.OperationId,
                    integrationEvent.ReceiptId,
                    integrationEvent.ConnectionId,
                    integrationEvent.PropertyId,
                    integrationEvent.OperationKind,
                    integrationEvent.Outcome,
                    integrationEvent.ReservationId,
                    integrationEvent.ErrorCode,
                }),
                BunkFyNotificationTags.ProviderAttention),
            cancellationToken);
    }
}
