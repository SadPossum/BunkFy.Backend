namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Naming;
using Gma.Modules.Notifications.Application.Ports;

internal sealed partial class OperationsNotificationsTenantTerminationContributor
{
    private static readonly NotificationScopeExportStore[] ExportStores =
    [
        NotificationScopeExportStore.UserNotifications,
        NotificationScopeExportStore.Preferences,
        NotificationScopeExportStore.DeliveryRoutes,
        NotificationScopeExportStore.TagDefinitions,
        NotificationScopeExportStore.Deliveries,
        NotificationScopeExportStore.DeliveryAttempts,
        NotificationScopeExportStore.TenantBroadcasts,
        NotificationScopeExportStore.TenantBroadcastReads,
        NotificationScopeExportStore.HistoryReferenceStates,
        NotificationScopeExportStore.HistoryCloseReceipts,
        NotificationScopeExportStore.HistoryBatchCloseOperations,
        NotificationScopeExportStore.HistoryBatchCloseReceipts
    ];

    public async Task<TenantTerminationContributionResult> ExportAsync(
        TenantTerminationExportRequest request,
        IDataRightsExportSink sink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sink);

        DateTimeOffset startedAtUtc = clock.UtcNow;
        if (!IsValid(request, scopeContext, startedAtUtc))
        {
            return Failed(
                "operations-notifications.termination.export-request-invalid",
                startedAtUtc);
        }

        _ = TenantIds.TryNormalize(
            request.Contribution.TenantId,
            out string? tenantId);
        WorkspaceTerminationFenceSnapshot? selectedFence =
            await this.ReadFenceAsync(cancellationToken)
                .ConfigureAwait(false);
        if (!Matches(request, selectedFence))
        {
            return Retry(
                "operations-notifications.termination.export-fence-unavailable",
                clock.UtcNow);
        }

        NotificationScopeSnapshot selected = await lifecycle.GetSnapshotAsync(
                tenantId!,
                cancellationToken)
            .ConfigureAwait(false);
        if (!TrySelectExportRevision(
                selected,
                out long selectedRevision,
                out bool isMissing))
        {
            return selected?.Status switch
            {
                NotificationScopeStatus.Closed => Failed(
                    "operations-notifications.termination.export-scope-closed",
                    clock.UtcNow),
                NotificationScopeStatus.ScopeUnavailable => Retry(
                    "operations-notifications.termination.export-scope-unavailable",
                    clock.UtcNow),
                _ => Failed(
                    "operations-notifications.termination.export-scope-invalid",
                    clock.UtcNow)
            };
        }

        ScopeExportResult export = isMissing
            ? ScopeExportResult.Success(0)
            : await this.ExportStoresAsync(
                    tenantId!,
                    selectedRevision,
                    request.Contribution.DeadlineUtc,
                    sink,
                    cancellationToken)
                .ConfigureAwait(false);
        if (export.Status == ScopeExportStatus.RetryRequired)
        {
            return Retry(
                export.Code,
                clock.UtcNow);
        }

        if (export.Status != ScopeExportStatus.Completed)
        {
            return Failed(export.Code, clock.UtcNow);
        }

        NotificationScopeSnapshot resulting = await lifecycle.GetSnapshotAsync(
                tenantId!,
                cancellationToken)
            .ConfigureAwait(false);
        WorkspaceTerminationFenceSnapshot? resultingFence =
            await this.ReadFenceAsync(cancellationToken)
                .ConfigureAwait(false);
        bool scopeStable = isMissing
            ? resulting is
            {
                Status: NotificationScopeStatus.Missing,
                Revision: 0
            }
            : resulting is
            {
                Status: NotificationScopeStatus.Open
            } && resulting.Revision == selectedRevision;
        if (!scopeStable ||
            !Matches(request, resultingFence) ||
            resultingFence!.Version != selectedFence!.Version)
        {
            return Retry(
                "operations-notifications.termination.export-revision-changed",
                clock.UtcNow);
        }

        DateTimeOffset completedAtUtc = clock.UtcNow;
        if (completedAtUtc > request.Contribution.DeadlineUtc)
        {
            return Retry(
                "operations-notifications.termination.export-deadline-expired",
                completedAtUtc);
        }

        return Completed(
            "operations-notifications.termination.exported",
            export.RecordCount,
            selectedRevision,
            resulting.Revision,
            completedAtUtc);
    }

    private async Task<ScopeExportResult> ExportStoresAsync(
        string tenantId,
        long selectedRevision,
        DateTimeOffset deadlineUtc,
        IDataRightsExportSink sink,
        CancellationToken cancellationToken)
    {
        long count = 0;
        foreach (NotificationScopeExportStore store in ExportStores)
        {
            string? cursor = null;
            while (true)
            {
                if (clock.UtcNow > deadlineUtc)
                {
                    return ScopeExportResult.Retry(
                        "operations-notifications.termination.export-deadline-expired");
                }

                NotificationScopeExportPage page = await lifecycle.ExportAsync(
                        new NotificationScopeExportRequest(
                            tenantId,
                            selectedRevision,
                            store,
                            cursor,
                            NotificationScopeLifecycleLimits.MaximumPageSize),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (page is null)
                {
                    return ScopeExportResult.Failed(
                        "operations-notifications.termination.export-response-invalid");
                }

                if (page.Status != NotificationScopeExportStatus.Completed)
                {
                    return page.Status switch
                    {
                        NotificationScopeExportStatus.Missing or
                        NotificationScopeExportStatus.Closed or
                        NotificationScopeExportStatus.Stale or
                        NotificationScopeExportStatus.ScopeUnavailable =>
                            ScopeExportResult.Retry(
                                "operations-notifications.termination.export-revision-changed"),
                        _ => ScopeExportResult.Failed(
                            "operations-notifications.termination.export-response-invalid")
                    };
                }

                if (!IsValidPage(page, store, selectedRevision, cursor))
                {
                    return ScopeExportResult.Failed(
                        "operations-notifications.termination.export-response-invalid");
                }

                try
                {
                    foreach (NotificationScopeExportRecord record in page.Records)
                    {
                        await sink.WriteAsync(
                                Map(store, record),
                                cancellationToken)
                            .ConfigureAwait(false);
                        count = checked(count + 1);
                        if (count >
                            TenantTerminationExportContract
                                .MaximumRecordsPerFragment)
                        {
                            return ScopeExportResult.Failed(
                                "operations-notifications.termination.export-volume-exceeded");
                        }
                    }
                }
                catch (InvalidDataException)
                {
                    return ScopeExportResult.Failed(
                        "operations-notifications.termination.export-record-invalid");
                }
                catch (OverflowException)
                {
                    return ScopeExportResult.Failed(
                        "operations-notifications.termination.export-volume-exceeded");
                }

                if (!page.HasMore)
                {
                    break;
                }

                cursor = page.NextCursor;
            }
        }

        return ScopeExportResult.Success(count);
    }

    private static bool IsValidPage(
        NotificationScopeExportPage page,
        NotificationScopeExportStore store,
        long selectedRevision,
        string? cursor)
    {
        if (page.ScopeRevision != selectedRevision ||
            page.Store != store ||
            page.Records is null ||
            page.Records.Count >
                NotificationScopeLifecycleLimits.MaximumPageSize ||
            (page.NextCursor is not null &&
             (page.NextCursor.Length is <= 0 or >
                NotificationScopeLifecycleLimits.MaximumCursorLength ||
              page.NextCursor.Any(char.IsControl))))
        {
            return false;
        }

        if (page.Records.Count == 0)
        {
            return !page.HasMore &&
                string.Equals(page.NextCursor, cursor, StringComparison.Ordinal);
        }

        return page.NextCursor is not null &&
            !string.Equals(page.NextCursor, cursor, StringComparison.Ordinal) &&
            (!page.HasMore ||
             page.Records.Count ==
                NotificationScopeLifecycleLimits.MaximumPageSize);
    }

    private static bool TrySelectExportRevision(
        NotificationScopeSnapshot? snapshot,
        out long revision,
        out bool isMissing)
    {
        revision = 0;
        isMissing = false;
        if (snapshot is null)
        {
            return false;
        }

        if (snapshot.Status == NotificationScopeStatus.Missing &&
            snapshot.Revision == 0)
        {
            isMissing = true;
            return true;
        }

        if (snapshot.Status == NotificationScopeStatus.Open &&
            snapshot.Revision is > 0 and < long.MaxValue)
        {
            revision = snapshot.Revision;
            return true;
        }

        return false;
    }

    private enum ScopeExportStatus
    {
        Failed = 0,
        Completed = 1,
        RetryRequired = 2
    }

    private sealed record ScopeExportResult(
        ScopeExportStatus Status,
        string Code,
        long RecordCount)
    {
        public static ScopeExportResult Success(long count) =>
            new(ScopeExportStatus.Completed, string.Empty, count);

        public static ScopeExportResult Retry(string code) =>
            new(ScopeExportStatus.RetryRequired, code, 0);

        public static ScopeExportResult Failed(string code) =>
            new(ScopeExportStatus.Failed, code, 0);
    }
}
