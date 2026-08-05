namespace BunkFy.Extensions.DataRights.AccessControl;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Modules.AccessControl.Contracts;

internal sealed partial class AccessControlTenantTerminationContributor
{
    private static readonly AccessControlScopeExportStore[] ExportStores =
    [
        AccessControlScopeExportStore.RoleAssignments,
        AccessControlScopeExportStore.Profiles,
        AccessControlScopeExportStore.ProfileAssignments,
        AccessControlScopeExportStore.ProfileChanges
    ];

    public async Task<TenantTerminationContributionResult> ExportAsync(
        TenantTerminationExportRequest request,
        IDataRightsExportSink sink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sink);

        DateTimeOffset startedAtUtc = clock.UtcNow;
        if (!IsValid(
                request,
                scopeContext,
                startedAtUtc,
                out AccessControlScopeCoordinate coordinate))
        {
            return Failed(
                "access-control.termination.export-request-invalid",
                startedAtUtc);
        }

        WorkspaceTerminationFenceSnapshot? selectedFence =
            await this.ReadFenceAsync(cancellationToken)
                .ConfigureAwait(false);
        if (!Matches(request, selectedFence))
        {
            return Retry(
                "access-control.termination.export-fence-unavailable",
                clock.UtcNow);
        }

        AccessControlScopeSnapshot selected = await lifecycle.GetSnapshotAsync(
                coordinate,
                cancellationToken)
            .ConfigureAwait(false);
        if (!TrySelectExportRevision(
                selected,
                out long selectedRevision,
                out bool isMissing))
        {
            return selected?.Status switch
            {
                AccessControlScopeStatus.Closed => Failed(
                    "access-control.termination.export-scope-closed",
                    clock.UtcNow),
                _ => Failed(
                    "access-control.termination.export-scope-invalid",
                    clock.UtcNow)
            };
        }

        ScopeExportResult export = isMissing
            ? ScopeExportResult.Success(0)
            : await this.ExportStoresAsync(
                    coordinate,
                    selectedRevision,
                    request.Contribution.DeadlineUtc,
                    sink,
                    cancellationToken)
                .ConfigureAwait(false);
        if (export.Status == ScopeExportStatus.RetryRequired)
        {
            return Retry(export.Code, clock.UtcNow);
        }

        if (export.Status != ScopeExportStatus.Completed)
        {
            return Failed(export.Code, clock.UtcNow);
        }

        AccessControlScopeSnapshot resulting =
            await lifecycle.GetSnapshotAsync(
                    coordinate,
                    cancellationToken)
                .ConfigureAwait(false);
        WorkspaceTerminationFenceSnapshot? resultingFence =
            await this.ReadFenceAsync(cancellationToken)
                .ConfigureAwait(false);
        bool scopeStable = isMissing
            ? resulting is
            {
                Status: AccessControlScopeStatus.Missing,
                Revision: 0,
                SelectedRevision: null
            }
            : resulting is
            {
                Status: AccessControlScopeStatus.Open,
                SelectedRevision: null
            } && resulting.Revision == selectedRevision;
        if (!scopeStable ||
            !Matches(request, resultingFence) ||
            resultingFence!.Version != selectedFence!.Version)
        {
            return Retry(
                "access-control.termination.export-revision-changed",
                clock.UtcNow);
        }

        DateTimeOffset completedAtUtc = clock.UtcNow;
        if (completedAtUtc > request.Contribution.DeadlineUtc)
        {
            return Retry(
                "access-control.termination.export-deadline-expired",
                completedAtUtc);
        }

        return Completed(
            "access-control.termination.exported",
            export.RecordCount,
            selectedRevision,
            resulting.Revision,
            completedAtUtc);
    }

    private async Task<ScopeExportResult> ExportStoresAsync(
        AccessControlScopeCoordinate coordinate,
        long selectedRevision,
        DateTimeOffset deadlineUtc,
        IDataRightsExportSink sink,
        CancellationToken cancellationToken)
    {
        long count = 0;
        foreach (AccessControlScopeExportStore store in ExportStores)
        {
            string? cursor = null;
            while (true)
            {
                if (clock.UtcNow > deadlineUtc)
                {
                    return ScopeExportResult.Retry(
                        "access-control.termination.export-deadline-expired");
                }

                AccessControlScopeExportPage page = await lifecycle.ExportAsync(
                        new AccessControlScopeExportRequest(
                            coordinate,
                            selectedRevision,
                            store,
                            cursor,
                            AccessControlScopeLifecycleLimits.MaximumPageSize),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (page is null)
                {
                    return ScopeExportResult.Failed(
                        "access-control.termination.export-response-invalid");
                }

                if (page.Status != AccessControlScopeExportStatus.Completed)
                {
                    return page.Status switch
                    {
                        AccessControlScopeExportStatus.Missing or
                        AccessControlScopeExportStatus.Closed or
                        AccessControlScopeExportStatus.Stale =>
                            ScopeExportResult.Retry(
                                "access-control.termination.export-revision-changed"),
                        _ => ScopeExportResult.Failed(
                            "access-control.termination.export-response-invalid")
                    };
                }

                if (!IsValidPage(page, store, selectedRevision, cursor))
                {
                    return ScopeExportResult.Failed(
                        "access-control.termination.export-response-invalid");
                }

                try
                {
                    foreach (AccessControlScopeExportRecord record in
                             page.Records)
                    {
                        await sink.WriteAsync(
                                Map(coordinate.RootScope, store, record),
                                cancellationToken)
                            .ConfigureAwait(false);
                        count = checked(count + 1);
                        if (count >
                            TenantTerminationExportContract
                                .MaximumRecordsPerFragment)
                        {
                            return ScopeExportResult.Failed(
                                "access-control.termination.export-volume-exceeded");
                        }
                    }
                }
                catch (InvalidDataException)
                {
                    return ScopeExportResult.Failed(
                        "access-control.termination.export-record-invalid");
                }
                catch (OverflowException)
                {
                    return ScopeExportResult.Failed(
                        "access-control.termination.export-volume-exceeded");
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
        AccessControlScopeExportPage page,
        AccessControlScopeExportStore store,
        long selectedRevision,
        string? cursor)
    {
        if (page.ScopeRevision != selectedRevision ||
            page.Store != store ||
            page.Records is null ||
            page.Records.Count >
                AccessControlScopeLifecycleLimits.MaximumPageSize ||
            (page.NextCursor is not null &&
             (page.NextCursor.Length is <= 0 or >
                AccessControlScopeLifecycleLimits.MaximumCursorLength ||
              page.NextCursor.Any(char.IsControl))))
        {
            return false;
        }

        if (page.Records.Count == 0)
        {
            return !page.HasMore &&
                string.Equals(
                    page.NextCursor,
                    cursor,
                    StringComparison.Ordinal);
        }

        return page.NextCursor is not null &&
            !string.Equals(
                page.NextCursor,
                cursor,
                StringComparison.Ordinal) &&
            (!page.HasMore ||
             page.Records.Count ==
                AccessControlScopeLifecycleLimits.MaximumPageSize);
    }

    private static bool TrySelectExportRevision(
        AccessControlScopeSnapshot? snapshot,
        out long revision,
        out bool isMissing)
    {
        revision = 0;
        isMissing = false;
        if (snapshot is null || snapshot.SelectedRevision.HasValue)
        {
            return false;
        }

        if (snapshot.Status == AccessControlScopeStatus.Missing &&
            snapshot.Revision == 0)
        {
            isMissing = true;
            return true;
        }

        if (snapshot.Status != AccessControlScopeStatus.Open ||
            snapshot.Revision is < 0 or long.MaxValue)
        {
            return false;
        }

        revision = snapshot.Revision;
        return true;
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
