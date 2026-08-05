namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.Modules.Ingestion.Contracts;
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
    public const string StaffInboxHistoryRecordType =
        "staff-inbox-history";
    public const string StaffInboxHistoryReferenceNamespace =
        "bunkfy-staff-inbox-history";
    public const string TenantInboxHistoryReferenceNamespace =
        "bunkfy-tenant-operations-history";
    public const string IngestionSourceLinkHistoryRecordType =
        "ingestion-source-link-history";
    public const string IngestionSourceLinkHistoryReferenceNamespace =
        "bunkfy-ingestion-source-link-history";
    public const string ReservationAccessExportCompanionKey =
        "operations-notifications-reservation-access-export";
    public const string ReservationAnonymisationCompanionKey =
        "operations-notifications-reservation-anonymisation";
    public const string StaffAccessExportCompanionKey =
        "operations-notifications-staff-access-export";
    public const string StaffAnonymisationCompanionKey =
        "operations-notifications-staff-anonymisation";
    public const string IngestionAccessExportCompanionKey =
        "operations-notifications-ingestion-access-export";
    public const string IngestionAnonymisationCompanionKey =
        "operations-notifications-ingestion-anonymisation";
    public const string StaffHistoryStateBindingKey =
        "operations-notifications.staff-inbox-history";

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

    public static NotificationHistoryReference ForStaff(
        string tenantId,
        Guid staffMemberId)
    {
        string normalizedTenant = ScopeIds.Normalize(
            tenantId,
            nameof(tenantId));
        if (staffMemberId == Guid.Empty)
        {
            throw new ArgumentException(
                "staffMemberId must be non-empty.",
                nameof(staffMemberId));
        }

        return NotificationHistoryReference.FromCanonicalCoordinate(
            StaffInboxHistoryReferenceNamespace,
            $"bunkfy-notification-reference/v1|{normalizedTenant}|" +
            $"staff|{staffMemberId:N}|" +
            $"{Modules.Staff.Contracts.StaffDataRightsCoordinates.Owner}|" +
            $"{Modules.Staff.Contracts.StaffDataRightsCoordinates.StaffMemberRecordType}|" +
            $"{staffMemberId:N}");
    }

    public static NotificationHistoryReference ForTenant(string tenantId)
    {
        string normalizedTenant = ScopeIds.Normalize(
            tenantId,
            nameof(tenantId));
        return NotificationHistoryReference.FromCanonicalCoordinate(
            TenantInboxHistoryReferenceNamespace,
            $"bunkfy-notification-reference/v1|{normalizedTenant}|" +
            $"{Owner}|tenant-operational-inbox");
    }

    public static NotificationHistoryReference ForIngestionSourceLink(
        string tenantId,
        Guid propertyId,
        Guid sourceLinkId)
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

        if (sourceLinkId == Guid.Empty)
        {
            throw new ArgumentException(
                "sourceLinkId must be non-empty.",
                nameof(sourceLinkId));
        }

        return NotificationHistoryReference.FromCanonicalCoordinate(
            IngestionSourceLinkHistoryReferenceNamespace,
            $"bunkfy-notification-reference/v1|{normalizedTenant}|" +
            $"property|{propertyId:N}|" +
            $"{IngestionDataRightsCoordinates.Owner}|" +
            $"{IngestionDataRightsCoordinates.ReservationSourceLinkRecordType}|" +
            $"{sourceLinkId:N}");
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
