namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Contracts;

internal static class NotificationHistoryDataRightsExportBuffer
{
    private const int PageSize =
        NotificationHistoryLifecycleLimits.MaximumPageSize;

    public static async Task<DataRightsSubjectExportResult> ExportAsync(
        INotificationHistoryLifecycle lifecycle,
        string tenantId,
        NotificationHistoryReference reference,
        long selectedVersion,
        int maximumRecords,
        Func<NotificationHistoryReferenceRecord, bool> accepts,
        Func<NotificationHistoryReferenceRecord, DataRightsExportRecord>
            map,
        IDataRightsExportSink sink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(accepts);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(sink);

        NotificationHistoryReferenceSnapshot initial =
            await lifecycle.GetSnapshotAsync(
                    tenantId,
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
            initial.Version != selectedVersion)
        {
            return DataRightsSubjectExportResult.Stale();
        }

        if (maximumRecords <= 0 ||
            initial.RecordCount > maximumRecords)
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
                        tenantId,
                        reference,
                        cursor,
                        PageSize,
                        cancellationToken)
                    .ConfigureAwait(false);
            if (page.Status != NotificationHistoryReferenceStatus.Open ||
                page.ReferenceVersion != selectedVersion)
            {
                return DataRightsSubjectExportResult.Stale();
            }

            if (page.Records.Count == 0 && page.HasMore)
            {
                return DataRightsSubjectExportResult.ScopeUnavailable();
            }

            foreach (NotificationHistoryReferenceRecord record in
                page.Records)
            {
                if (record.StreamSequence <= previousSequence ||
                    !notificationIds.Add(record.NotificationId) ||
                    !accepts(record))
                {
                    return DataRightsSubjectExportResult.ScopeUnavailable();
                }

                try
                {
                    buffered.Add(map(record));
                }
                catch (InvalidDataException)
                {
                    return DataRightsSubjectExportResult.ScopeUnavailable();
                }

                if (buffered.Count > maximumRecords)
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
                    tenantId,
                    reference,
                    cancellationToken)
                .ConfigureAwait(false);
        if (final.Status != NotificationHistoryReferenceStatus.Open ||
            final.Version != selectedVersion ||
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
}
