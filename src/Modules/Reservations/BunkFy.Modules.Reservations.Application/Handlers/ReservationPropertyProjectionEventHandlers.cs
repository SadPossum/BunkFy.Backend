namespace BunkFy.Modules.Reservations.Application.Handlers;

using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Messaging;

[IntegrationEventHandler(ReservationsModuleMetadata.PropertyCreatedHandlerName)]
internal sealed class ReservationPropertyCreatedHandler(IReservationArrivalReminderRepository reminders)
    : IIntegrationEventHandler<PropertyCreatedIntegrationEvent>
{
    public Task HandleAsync(PropertyCreatedIntegrationEvent e, CancellationToken cancellationToken) =>
        reminders.ApplyPropertyAsync(
            new(e.ScopeId, e.PropertyId, e.TimeZoneId, e.Status == PropertyStatus.Active, e.PropertyVersion, e.OccurredAtUtc),
            cancellationToken);
}

[IntegrationEventHandler(ReservationsModuleMetadata.PropertyUpdatedHandlerName)]
internal sealed class ReservationPropertyUpdatedHandler(IReservationArrivalReminderRepository reminders)
    : IIntegrationEventHandler<PropertyUpdatedIntegrationEvent>
{
    public Task HandleAsync(PropertyUpdatedIntegrationEvent e, CancellationToken cancellationToken) =>
        reminders.ApplyPropertyAsync(
            new(e.ScopeId, e.PropertyId, e.TimeZoneId, e.Status == PropertyStatus.Active, e.PropertyVersion, e.OccurredAtUtc),
            cancellationToken);
}

[IntegrationEventHandler(ReservationsModuleMetadata.PropertyRetiredHandlerName)]
internal sealed class ReservationPropertyRetiredHandler(IReservationArrivalReminderRepository reminders)
    : IIntegrationEventHandler<PropertyRetiredIntegrationEvent>
{
    public Task HandleAsync(PropertyRetiredIntegrationEvent e, CancellationToken cancellationToken) =>
        reminders.ApplyPropertyAsync(
            new(e.ScopeId, e.PropertyId, null, IsActive: false, e.PropertyVersion, e.OccurredAtUtc),
            cancellationToken);
}
