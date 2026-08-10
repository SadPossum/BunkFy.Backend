namespace BunkFy.Extensions.Operations.Notifications;

using System.Globalization;
using System.Text.Json;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Scoping;
using Gma.Modules.Notifications.Contracts;

internal sealed class
    OperationsNotificationsStaffDataRightsExportContributor(
        INotificationHistoryLifecycle lifecycle,
        IScopeContext scopeContext)
    : IDataRightsSubjectExportContributor
{
    internal const int MaximumRecords =
        OperationsNotificationsDataRightsReceipt.StaffMaximumRecords;

    public string OwnerKey =>
        OperationsNotificationsDataRightsCoordinates.Owner;

    public IReadOnlyCollection<DataRightsCaseType> SupportedCaseTypes { get; } =
        [DataRightsCaseType.StaffRights];

    public DataRightsExportDescriptor Descriptor =>
        OperationsNotificationsStaffDataRightsExportSchema.Descriptor;

    public async Task<DataRightsSubjectExportResult> ExportAsync(
        DataRightsSubjectExportRequest request,
        IDataRightsExportSink sink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sink);

        if (!OperationsNotificationsDataRightsValidation
                .IsStaffTenantScope(
                    scopeContext,
                    request.TenantId,
                    request.CaseType,
                    request.PropertyId))
        {
            return DataRightsSubjectExportResult.ScopeUnavailable();
        }

        if (!OperationsNotificationsDataRightsValidation
                .IsStaffHistoryCoordinate(request.Coordinate))
        {
            return DataRightsSubjectExportResult.NotFound();
        }

        NotificationHistoryReference reference =
            OperationsNotificationsDataRightsCoordinates.ForStaff(
                request.TenantId,
                request.Coordinate.RecordId);
        return await NotificationHistoryDataRightsExportBuffer.ExportAsync(
                lifecycle,
                request.TenantId,
                reference,
                request.Coordinate.RecordVersion,
                MaximumRecords,
                IsKnownNotification,
                record =>
                    OperationsNotificationsStaffDataRightsExportSchema
                        .CreateRecord(Map(record)),
                sink,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static StaffNotificationHistoryDataRightsExport Map(
        NotificationHistoryReferenceRecord record) =>
        new(
            record.NotificationId,
            record.SourceModule,
            record.NotificationName,
            record.NotificationVersion,
            record.Title,
            record.Body,
            record.Severity,
            record.StreamSequence,
            record.OccurredAtUtc,
            record.CreatedAtUtc,
            record.Payload.Clone(),
            record.Tags.ToArray(),
            record.DeliveryPolicy);

    private static bool IsKnownNotification(
        NotificationHistoryReferenceRecord record)
    {
        if (record.NotificationVersion != 1)
        {
            return false;
        }

        return (record.SourceModule, record.NotificationName) switch
        {
            (PropertiesModuleMetadata.Name, "property-retired") =>
                IsPropertyPayload(record.Payload),
            (InventoryModuleMetadata.Name,
                "manual-inventory-block-created") =>
                IsInventoryBlockCreatedPayload(record.Payload),
            (InventoryModuleMetadata.Name,
                "manual-inventory-block-released") =>
                IsInventoryBlockReleasedPayload(record.Payload),
            (InventoryModuleMetadata.Name, "room-sales-mode-changed") =>
                IsRoomPayload(record.Payload),
            (ReservationsModuleMetadata.Name,
                "reservation-confirmed") =>
                IsReservationPayload(record.Payload),
            (ReservationsModuleMetadata.Name,
                "reservation-arrival-soon") =>
                IsReservationPayload(record.Payload),
            (ReservationsModuleMetadata.Name,
                "reservation-allocation-rejected") =>
                IsReservationPayload(record.Payload),
            (ReservationsModuleMetadata.Name,
                "reservation-cancelled") =>
                IsReservationPayload(record.Payload),
            (ReservationsModuleMetadata.Name,
                "reservation-no-show") =>
                IsReservationPayload(record.Payload),
            (ReservationsModuleMetadata.Name,
                "provider-reservation-operation-needs-attention") =>
                IsProviderAttentionPayload(record.Payload),
            (StaffModuleMetadata.Name, "staff-property-assigned") =>
                IsStaffPayload(record.Payload),
            (StaffModuleMetadata.Name, "staff-property-unassigned") =>
                IsStaffPayload(record.Payload),
            (StaffModuleMetadata.Name, "staff-lifecycle-changed") =>
                IsStaffPayload(record.Payload),
            (DataRightsModuleMetadata.Name,
                DataRightsResponseDeadlineNotificationHandler
                    .DueSoonNotificationName) =>
                IsDataRightsCasePayload(record.Payload),
            (DataRightsModuleMetadata.Name,
                DataRightsResponseDeadlineNotificationHandler
                    .OverdueNotificationName) =>
                IsDataRightsCasePayload(record.Payload),
            _ => false
        };
    }

    private static bool IsPropertyPayload(JsonElement payload) =>
        HasExactProperties(payload, "PropertyId") &&
        HasGuid(payload, "PropertyId");

    private static bool IsReservationPayload(JsonElement payload) =>
        HasExactProperties(payload, "PropertyId", "ReservationId") &&
        HasGuid(payload, "PropertyId") &&
        HasGuid(payload, "ReservationId");

    private static bool IsDataRightsCasePayload(JsonElement payload) =>
        HasExactProperties(payload, "CaseId", "PropertyId") &&
        HasGuid(payload, "CaseId") &&
        HasGuid(payload, "PropertyId");

    private static bool IsInventoryBlockReleasedPayload(
        JsonElement payload) =>
        HasExactProperties(payload, "BlockGroupId", "PropertyId") &&
        HasGuid(payload, "BlockGroupId") &&
        HasGuid(payload, "PropertyId");

    private static bool IsRoomPayload(JsonElement payload) =>
        HasExactProperties(payload, "PropertyId", "RoomId") &&
        HasGuid(payload, "PropertyId") &&
        HasGuid(payload, "RoomId");

    private static bool IsStaffPayload(JsonElement payload) =>
        HasExactProperties(payload, "StaffMemberId") &&
        HasGuid(payload, "StaffMemberId");

    private static bool IsInventoryBlockCreatedPayload(
        JsonElement payload) =>
        HasExactProperties(
            payload,
            "Arrival",
            "BlockGroupId",
            "Departure",
            "PropertyId") &&
        HasDate(payload, "Arrival") &&
        HasGuid(payload, "BlockGroupId") &&
        HasDate(payload, "Departure") &&
        HasGuid(payload, "PropertyId");

    private static bool IsProviderAttentionPayload(JsonElement payload) =>
        HasExactProperties(
            payload,
            "ConnectionId",
            "PropertyId",
            "ReceiptId",
            "ReservationId") &&
        HasGuid(payload, "ConnectionId") &&
        HasGuid(payload, "PropertyId") &&
        HasGuid(payload, "ReceiptId") &&
        HasOptionalGuid(payload, "ReservationId");

    private static bool HasExactProperties(
        JsonElement payload,
        params string[] expected)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        string[] names = payload.EnumerateObject()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return names.SequenceEqual(
            expected.Order(StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    private static bool HasGuid(
        JsonElement payload,
        string propertyName) =>
        payload.TryGetProperty(
            propertyName,
            out JsonElement property) &&
        property.ValueKind == JsonValueKind.String &&
        property.TryGetGuid(out Guid value) &&
        value != Guid.Empty;

    private static bool HasOptionalGuid(
        JsonElement payload,
        string propertyName) =>
        payload.TryGetProperty(
            propertyName,
            out JsonElement property) &&
        (property.ValueKind == JsonValueKind.Null ||
         (property.ValueKind == JsonValueKind.String &&
          property.TryGetGuid(out Guid value) &&
          value != Guid.Empty));

    private static bool HasDate(
        JsonElement payload,
        string propertyName) =>
        payload.TryGetProperty(
            propertyName,
            out JsonElement property) &&
        property.ValueKind == JsonValueKind.String &&
        DateOnly.TryParseExact(
            property.GetString(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);
}
