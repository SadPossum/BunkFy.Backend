namespace BunkFy.Extensions.Operations.Notifications;

using System.Text.Json;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Scoping;
using Gma.Modules.Notifications.Application.Ports;
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
                .IsReservationHistoryCoordinate(request.Coordinate))
        {
            return DataRightsSubjectExportResult.NotFound();
        }

        NotificationHistoryReference reference =
            OperationsNotificationsDataRightsCoordinates.ForReservation(
                request.TenantId,
                request.PropertyId!.Value,
                request.Coordinate.RecordId);
        return await NotificationHistoryDataRightsExportBuffer.ExportAsync(
                lifecycle,
                request.TenantId,
                reference,
                request.Coordinate.RecordVersion,
                MaximumRecords,
                record => PayloadMatches(
                    record.Payload,
                    request.PropertyId.Value,
                    request.Coordinate.RecordId),
                record =>
                    OperationsNotificationsDataRightsExportSchema
                        .CreateRecord(Map(record)),
                sink,
                cancellationToken)
            .ConfigureAwait(false);
    }

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

    private static bool PayloadMatches(
        JsonElement payload,
        Guid propertyId,
        Guid reservationId) =>
        payload.ValueKind == JsonValueKind.Object &&
        TryReadGuid(payload, "PropertyId", out Guid payloadPropertyId) &&
        payloadPropertyId == propertyId &&
        TryReadGuid(payload, "ReservationId", out Guid payloadReservationId) &&
        payloadReservationId == reservationId;

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
