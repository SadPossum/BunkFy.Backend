namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Naming;
using Gma.Modules.Notifications.Contracts;

internal static class OperationsNotificationsDataRightsCoordinates
{
    public const string Owner = "operations-notifications";
    public const string ReservationHistoryRecordType =
        "reservation-history";
    public const string NotificationCopyRecordType =
        "notification-copy";
    public const string ReservationHistoryReferenceNamespace =
        "bunkfy-reservation-history";
    public const string ReservationAccessExportCompanionKey =
        "operations-notifications-reservation-access-export";
    public const string ReservationAnonymisationCompanionKey =
        "operations-notifications-reservation-anonymisation";

    public static NotificationHistoryReference ForReservation(
        string tenantId,
        Guid propertyId,
        Guid reservationId)
    {
        string normalizedTenant = ScopeIds.Normalize(
            tenantId,
            nameof(tenantId));
        if (propertyId == Guid.Empty)
        {
            throw new ArgumentException(
                "propertyId must be non-empty.",
                nameof(propertyId));
        }

        if (reservationId == Guid.Empty)
        {
            throw new ArgumentException(
                "reservationId must be non-empty.",
                nameof(reservationId));
        }

        return NotificationHistoryReference.FromCanonicalCoordinate(
            ReservationHistoryReferenceNamespace,
            $"bunkfy-notification-reference/v1|{normalizedTenant}|" +
            $"property|{propertyId:N}|" +
            $"{ReservationsDataRightsCoordinates.Owner}|" +
            $"{ReservationsDataRightsCoordinates.ReservationRecordType}|" +
            $"{reservationId:N}");
    }

    public static IReadOnlyList<NotificationHistoryReference>
        FromPayload(
            string tenantId,
            IOperationalNotificationPayload payload) =>
        payload switch
        {
            ReservationNotificationPayload reservation =>
            [
                ForReservation(
                    tenantId,
                    reservation.PropertyId,
                    reservation.ReservationId)
            ],
            ProviderAttentionNotificationPayload provider
                when provider.ReservationId is Guid reservationId =>
            [
                ForReservation(
                    tenantId,
                    provider.PropertyId,
                    reservationId)
            ],
            _ => []
        };
}
