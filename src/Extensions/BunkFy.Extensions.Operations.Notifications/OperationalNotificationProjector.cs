namespace BunkFy.Extensions.Operations.Notifications;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.AccessControl;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Contracts;
using Gma.Modules.Organizations.Application.Ports;

internal sealed class OperationalNotificationProjector(
    IStaffPropertyAudienceReader audienceReader,
    IWorkspaceOwnerNotificationAudienceReader workspaceOwnerAudienceReader,
    IOrganizationAccessCandidateFilter organizationAccess,
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
        IReadOnlyList<string> propertyStaffRecipients = await audienceReader
            .ListActiveAuthSubjectIdsAsync(scopeId, propertyId, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<string> workspaceOwnerRecipients = await workspaceOwnerAudienceReader
            .ListAuthSubjectIdsAsync(scopeId, cancellationToken)
            .ConfigureAwait(false);

        string[] candidates = propertyStaffRecipients
            .Concat(workspaceOwnerRecipients)
            .Distinct(StringComparer.Ordinal)
            .Where(recipient => !IsInitiatingUser(recipient, notification.ActorId))
            .Order(StringComparer.Ordinal)
            .ToArray();
        IReadOnlyList<string> recipients = await this.FilterActiveMembersAsync(
                scopeId,
                candidates,
                cancellationToken)
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

        if (IsInitiatingUser(recipient, notification.ActorId))
        {
            return;
        }

        IReadOnlyList<string> recipients = await this.FilterActiveMembersAsync(
                scopeId,
                [recipient],
                cancellationToken)
            .ConfigureAwait(false);
        if (recipients.Count == 0)
        {
            return;
        }

        await this.ProjectAsync(
                sourceEventId,
                scopeId,
                occurredAtUtc,
                recipients[0],
                notification,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<string>> FilterActiveMembersAsync(
        string scopeId,
        string[] candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Length == 0)
        {
            return [];
        }

        if (!Guid.TryParse(scopeId, out Guid organizationId) || organizationId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "A BunkFy operational notification scope must be an organization id.");
        }

        List<string> allowed = new(candidates.Length);
        foreach (string[] batch in candidates.Chunk(IOrganizationAccessCandidateFilter.MaximumCandidateCount))
        {
            IReadOnlyList<string> filtered = await organizationAccess
                .FilterAllowedAsync(organizationId, batch, cancellationToken)
                .ConfigureAwait(false);
            allowed.AddRange(filtered);
        }

        return allowed;
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
                JsonSerializer.Serialize(notification.Payload, notification.Payload.GetType()),
                notification.Tags,
                NotificationDeliveryPolicy.RespectPreferences),
            cancellationToken);

    internal static Guid CreateNotificationId(Guid sourceEventId, string recipient, string notificationName)
    {
        string identity = $"{sourceEventId:D}|{recipient.Trim()}|{notificationName}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return new Guid(hash.AsSpan(0, 16));
    }

    internal static bool IsInitiatingUser(string recipient, string? actorId)
    {
        const char separator = ':';
        string normalizedActor = actorId?.Trim() ?? string.Empty;
        int separatorIndex = normalizedActor.IndexOf(separator);
        return separatorIndex > 0 &&
               string.Equals(
                   normalizedActor[..separatorIndex],
                   AccessSubjectKindNames.User,
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(
                   normalizedActor[(separatorIndex + 1)..],
                   recipient.Trim(),
                   StringComparison.Ordinal);
    }
}
