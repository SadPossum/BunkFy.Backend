namespace BunkFy.Extensions.Operations.Notifications;

using System.Text.Json;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Scoping;
using Gma.Modules.Notifications.Contracts;

internal sealed class OperationsNotificationsDataRightsExportContributor(
    INotificationHistoryLifecycle lifecycle,
    IScopeContext scopeContext)
    : IDataRightsSubjectExportContributor
{
    internal const int MaximumRecords =
        OperationsNotificationsDataRightsReceipt.MaximumRecords;

    public string OwnerKey =>
        OperationsNotificationsDataRightsCoordinates.Owner;

    public IReadOnlyCollection<DataRightsCaseType> SupportedCaseTypes { get; } =
        [DataRightsCaseType.GuestRights];

    public DataRightsExportDescriptor Descriptor =>
        OperationsNotificationsDataRightsExportSchema.Descriptor;

    public async Task<DataRightsSubjectExportResult> ExportAsync(
        DataRightsSubjectExportRequest request,
        IDataRightsExportSink sink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sink);

        if (!OperationsNotificationsDataRightsValidation.IsGuestPropertyScope(
                scopeContext,
                request.TenantId,
                request.CaseType,
                request.PropertyId))
        {
            return DataRightsSubjectExportResult.ScopeUnavailable();
        }

        if (!OperationsNotificationsDataRightsValidation
                .TryCreateGuestHistoryReference(
                    request.TenantId,
                    request.PropertyId!.Value,
                    request.Coordinate,
                    out NotificationHistoryReference? reference))
        {
            return DataRightsSubjectExportResult.NotFound();
        }

        return await NotificationHistoryDataRightsExportBuffer.ExportAsync(
                lifecycle,
                request.TenantId,
                reference!,
                request.Coordinate.RecordVersion,
                MaximumRecords,
                record => RecordMatches(
                    record,
                    request.PropertyId.Value,
                    request.Coordinate),
                record =>
                    OperationsNotificationsDataRightsExportSchema
                        .CreateRecord(Map(record)),
                sink,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool RecordMatches(
        NotificationHistoryReferenceRecord record,
        Guid propertyId,
        DataRightsSubjectCoordinate coordinate) =>
        coordinate.RecordType switch
        {
            OperationsNotificationsDataRightsCoordinates
                .ReservationHistoryRecordType =>
                ReservationPayloadMatches(
                    record.Payload,
                    propertyId,
                    coordinate.RecordId),
            OperationsNotificationsDataRightsCoordinates
                .IngestionSourceLinkHistoryRecordType =>
                ProviderAttentionRecordMatches(record, propertyId),
            _ => false
        };

    private static ReservationNotificationHistoryDataRightsExport Map(
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

    private static bool ReservationPayloadMatches(
        JsonElement payload,
        Guid propertyId,
        Guid reservationId) =>
        payload.ValueKind == JsonValueKind.Object &&
        TryReadGuid(payload, "PropertyId", out Guid payloadPropertyId) &&
        payloadPropertyId == propertyId &&
        TryReadGuid(payload, "ReservationId", out Guid payloadReservationId) &&
        payloadReservationId == reservationId;

    private static bool ProviderAttentionRecordMatches(
        NotificationHistoryReferenceRecord record,
        Guid propertyId) =>
        string.Equals(
            record.SourceModule,
            ReservationsModuleMetadata.Name,
            StringComparison.Ordinal) &&
        string.Equals(
            record.NotificationName,
            "provider-reservation-operation-needs-attention",
            StringComparison.Ordinal) &&
        record.NotificationVersion == 1 &&
        HasExactProperties(
            record.Payload,
            "ConnectionId",
            "PropertyId",
            "ReceiptId",
            "ReservationId") &&
        TryReadGuid(
            record.Payload,
            "PropertyId",
            out Guid payloadPropertyId) &&
        payloadPropertyId == propertyId &&
        TryReadGuid(record.Payload, "ConnectionId", out _) &&
        TryReadGuid(record.Payload, "ReceiptId", out _) &&
        HasOptionalGuid(record.Payload, "ReservationId");

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

    private static bool TryReadGuid(
        JsonElement payload,
        string propertyName,
        out Guid value)
    {
        value = Guid.Empty;
        return payload.TryGetProperty(propertyName, out JsonElement property) &&
            property.ValueKind == JsonValueKind.String &&
            property.TryGetGuid(out value) &&
            value != Guid.Empty;
    }
}
