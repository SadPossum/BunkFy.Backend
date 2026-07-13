namespace BunkFy.Extensions.Operations.Notifications;

using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.Staff.Contracts;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Contracts;

internal sealed class OperationalNotificationProjector(
    IStaffPropertyAudienceReader audienceReader,
    IUserNotificationRequestProjector notificationProjector)
{
    public async Task ProjectForPropertyAsync(
        Guid sourceEventId,
        string scopeId,
        DateTimeOffset occurredAtUtc,
        Guid propertyId,
        OperationalNotification notification,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> recipients = await audienceReader
            .ListActiveAuthSubjectIdsAsync(scopeId, propertyId, cancellationToken)
            .ConfigureAwait(false);

        foreach (string recipient in recipients)
        {
            await this.ProjectAsync(
                    sourceEventId,
                    scopeId,
                    occurredAtUtc,
                    recipient,
                    notification,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task ProjectForStaffMemberAsync(
        Guid sourceEventId,
        string scopeId,
        DateTimeOffset occurredAtUtc,
        Guid staffMemberId,
        OperationalNotification notification,
        CancellationToken cancellationToken)
    {
        string? recipient = await audienceReader
            .GetAuthSubjectIdAsync(scopeId, staffMemberId, cancellationToken)
            .ConfigureAwait(false);
        if (recipient is null)
        {
            return;
        }

        await this.ProjectAsync(
                sourceEventId,
                scopeId,
                occurredAtUtc,
                recipient,
                notification,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private Task ProjectAsync(
        Guid sourceEventId,
        string scopeId,
        DateTimeOffset occurredAtUtc,
        string recipient,
        OperationalNotification notification,
        CancellationToken cancellationToken) =>
        notificationProjector.ProjectAsync(
            new UserNotificationRequestedIntegrationEventV2(
                CreateNotificationId(sourceEventId, recipient, notification.Name),
                scopeId,
                occurredAtUtc,
                recipient,
                notification.SourceModule,
                notification.Name,
                1,
                notification.Title,
                notification.Body,
                notification.Severity,
                notification.PayloadJson,
                notification.Tags,
                NotificationDeliveryPolicy.RespectPreferences),
            cancellationToken);

    internal static Guid CreateNotificationId(Guid sourceEventId, string recipient, string notificationName)
    {
        string identity = $"{sourceEventId:D}|{recipient.Trim()}|{notificationName}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return new Guid(hash.AsSpan(0, 16));
    }
}
