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
    private const int PageSize =
        NotificationHistoryLifecycleLimits.MaximumPageSize;

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
        NotificationHistoryReferenceSnapshot initial =
            await lifecycle.GetSnapshotAsync(
                    request.TenantId,
                    reference,
                    cancellationToken)
                .ConfigureAwait(false);
        if (initial.Status is
            NotificationHistoryReferenceStatus.Missing or
            NotificationHistoryReferenceStatus.Closed)
        {
            return DataRightsSubjectExportResult.NotFound();
        }

        if (initial.Status != NotificationHistoryReferenceStatus.Open ||
            initial.Version != request.Coordinate.RecordVersion)
        {
            return DataRightsSubjectExportResult.Stale();
        }

        if (initial.RecordCount > MaximumRecords)
        {
            return DataRightsSubjectExportResult.ScopeUnavailable();
        }

        List<DataRightsExportRecord> buffered =
            new(initial.RecordCount);
        HashSet<Guid> notificationIds = [];
        long cursor = 0;
        long previousSequence = 0;
        while (true)
        {
            NotificationHistoryReferencePage page =
                await lifecycle.ListAsync(
                        request.TenantId,
                        reference,
                        cursor,
                        PageSize,
                        cancellationToken)
                    .ConfigureAwait(false);
            if (page.Status != NotificationHistoryReferenceStatus.Open ||
                page.ReferenceVersion != request.Coordinate.RecordVersion)
            {
                return DataRightsSubjectExportResult.Stale();
            }

            if (page.Records.Count == 0 && page.HasMore)
            {
                return DataRightsSubjectExportResult.ScopeUnavailable();
            }

            foreach (NotificationHistoryReferenceRecord record in page.Records)
            {
                if (record.StreamSequence <= previousSequence ||
                    !notificationIds.Add(record.NotificationId) ||
                    !PayloadMatches(
                        record.Payload,
                        request.PropertyId.Value,
                        request.Coordinate.RecordId))
                {
                    return DataRightsSubjectExportResult.ScopeUnavailable();
                }

                DataRightsExportRecord exportRecord;
                try
                {
                    exportRecord =
                        OperationsNotificationsDataRightsExportSchema
                            .CreateRecord(Map(record));
                }
                catch (InvalidDataException)
                {
                    return DataRightsSubjectExportResult.ScopeUnavailable();
                }

                buffered.Add(exportRecord);
                if (buffered.Count > MaximumRecords)
                {
                    return DataRightsSubjectExportResult.ScopeUnavailable();
                }

                previousSequence = record.StreamSequence;
            }

            if (!page.HasMore)
            {
                break;
            }

            if (page.NextStreamSequence <= cursor)
            {
                return DataRightsSubjectExportResult.ScopeUnavailable();
            }

            cursor = page.NextStreamSequence;
        }

        NotificationHistoryReferenceSnapshot final =
            await lifecycle.GetSnapshotAsync(
                    request.TenantId,
                    reference,
                    cancellationToken)
                .ConfigureAwait(false);
        if (final.Status != NotificationHistoryReferenceStatus.Open ||
            final.Version != request.Coordinate.RecordVersion ||
            final.RecordCount != buffered.Count ||
            (buffered.Count > 0 &&
             final.LatestStreamSequence != previousSequence))
        {
            return DataRightsSubjectExportResult.Stale();
        }

        foreach (DataRightsExportRecord record in buffered)
        {
            await sink.WriteAsync(record, cancellationToken)
                .ConfigureAwait(false);
        }

        return DataRightsSubjectExportResult.Success(buffered.Count);
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
