namespace BunkFy.Extensions.Operations.Notifications;

using System.Text.Json;
using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Messaging;
using Gma.Modules.Notifications.Contracts;

[IntegrationEventHandler("bunkfy-staff-property-assignment-notification", RequiresExplicitProducerBinding = true)]
internal sealed class StaffPropertyAssignmentChangedNotificationHandler(OperationalNotificationProjector projector)
    : IIntegrationEventHandler<StaffPropertyAssignmentChangedIntegrationEvent>
{
    public Task HandleAsync(
        StaffPropertyAssignmentChangedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        projector.ProjectForStaffMemberAsync(
            integrationEvent.EventId,
            integrationEvent.ScopeId,
            integrationEvent.OccurredAtUtc,
            integrationEvent.StaffMemberId,
            new OperationalNotification(
                StaffModuleMetadata.Name,
                integrationEvent.IsCurrent ? "staff-property-assigned" : "staff-property-unassigned",
                integrationEvent.IsCurrent ? "Property assignment added" : "Property assignment ended",
                integrationEvent.IsCurrent
                    ? "You were assigned to a property."
                    : "Your property assignment ended.",
                NotificationSeverity.Info,
                JsonSerializer.Serialize(new
                {
                    integrationEvent.StaffMemberId,
                    integrationEvent.AssignmentId,
                    integrationEvent.PropertyId,
                    integrationEvent.IsCurrent,
                    integrationEvent.IsPrimary,
                    integrationEvent.EffectiveFrom,
                    integrationEvent.EffectiveTo,
                    integrationEvent.StaffVersion,
                }),
                BunkFyNotificationTags.StaffActivity),
            cancellationToken);
}

[IntegrationEventHandler("bunkfy-staff-lifecycle-notification", RequiresExplicitProducerBinding = true)]
internal sealed class StaffMemberLifecycleChangedNotificationHandler(OperationalNotificationProjector projector)
    : IIntegrationEventHandler<StaffMemberLifecycleChangedIntegrationEvent>
{
    public Task HandleAsync(
        StaffMemberLifecycleChangedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        projector.ProjectForStaffMemberAsync(
            integrationEvent.EventId,
            integrationEvent.ScopeId,
            integrationEvent.OccurredAtUtc,
            integrationEvent.StaffMemberId,
            new OperationalNotification(
                StaffModuleMetadata.Name,
                "staff-lifecycle-changed",
                "Staff account status changed",
                $"Your staff account status changed to {integrationEvent.Status} effective {integrationEvent.EffectiveOn:yyyy-MM-dd}.",
                integrationEvent.Status == StaffStatus.Active
                    ? NotificationSeverity.Success
                    : NotificationSeverity.Warning,
                JsonSerializer.Serialize(new
                {
                    integrationEvent.StaffMemberId,
                    integrationEvent.Status,
                    integrationEvent.EffectiveOn,
                    integrationEvent.StaffVersion,
                }),
                BunkFyNotificationTags.StaffActivity),
            cancellationToken);
}
